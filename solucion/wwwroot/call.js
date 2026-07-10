// ─────────────────────────────────────────────────────────────────────────────
// call.js — Videollamada 1:1 sobre el Mini-Teams.
// El audio/vídeo viaja DIRECTO entre navegadores (WebRTC). El hub solo hace de
// "central telefónica": reenvía la oferta/respuesta (SDP) y los candidatos (ICE).
// Módulo aparte a propósito: si no lo cargas, el chat sigue igual.
// ─────────────────────────────────────────────────────────────────────────────
(function () {
  const $ = s => document.querySelector(s);
  const RTC = { iceServers: [{ urls: 'stun:stun.l.google.com:19302' }] };

  let conn = null, me = '';
  let pc = null;            // conexión WebRTC actual
  let localStream = null;
  let peer = '';            // con quién hablo
  let pending = '';         // llamada entrante sin contestar

  // app.js llama a esto justo antes de conn.start()
  window.initCall = function (connection, myName) {
    conn = connection;
    me = myName;
    buildUI();

    conn.on('IncomingCall', from => showIncoming(from));
    conn.on('CallAccepted', async from => { if (from === peer) await startPeer(true); });
    conn.on('CallDeclined', from => { if (from === peer) endCall(`${from} no puede contestar`); });
    conn.on('CallEnded',    from => { if (from === peer || from === pending) endCall(`${from} colgó`); });
    conn.on('ReceiveSignal', (from, data) => onSignal(from, data));

    // Botón 📞 en cada usuario (delegación sobre la lista)
    $('#users').addEventListener('click', e => {
      const btn = e.target.closest('.call-btn');
      if (btn) call(btn.dataset.user);
    });
  };

  // ── Iniciar / recibir ──
  async function call(target) {
    if (!target || target === me || pc || peer) return;
    peer = target;
    setStatus(`Llamando a ${target}…`);
    $('#callTitle').textContent = `📞 Videollamada`;
    $('#incoming').classList.add('hidden');
    show(true);
    await conn.invoke('CallUser', target);
  }

  function showIncoming(from) {
    if (pc || peer) { conn.invoke('DeclineCall', from); return; }   // ocupado
    pending = from;
    $('#callTitle').textContent = `📞 ${from} te está llamando…`;
    setStatus('');
    $('#incoming').classList.remove('hidden');
    show(true);
  }

  async function accept() {
    peer = pending; pending = '';
    $('#incoming').classList.add('hidden');
    setStatus(`Conectando con ${peer}…`);
    await conn.invoke('AcceptCall', peer);   // el que llamó creará la oferta
  }

  async function decline() {
    if (pending) { await conn.invoke('DeclineCall', pending); pending = ''; }
    endCall();
  }

  // ── WebRTC ──
  async function startPeer(isCaller) {
    try {
      localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
    } catch (err) {
      endCall('No pude acceder a la cámara/micrófono');
      return;
    }
    $('#localVideo').srcObject = localStream;
    pc = new RTCPeerConnection(RTC);
    localStream.getTracks().forEach(t => pc.addTrack(t, localStream));
    pc.ontrack = e => { $('#remoteVideo').srcObject = e.streams[0]; };
    pc.onicecandidate = e => { if (e.candidate) sig({ ice: e.candidate }); };
    setStatus(`En llamada con ${peer}`);
    if (isCaller) {
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      sig({ sdp: pc.localDescription });
    }
  }

  async function onSignal(from, data) {
    if (from !== peer) return;
    const msg = JSON.parse(data);
    if (msg.sdp) {
      if (!pc) await startPeer(false);                 // el que recibe arma el pc al llegar la oferta
      if (!pc) return;                                 // (si falló la cámara)
      await pc.setRemoteDescription(msg.sdp);
      if (msg.sdp.type === 'offer') {
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        sig({ sdp: pc.localDescription });
      }
    } else if (msg.ice) {
      try { await pc.addIceCandidate(msg.ice); } catch { /* candidato tardío */ }
    }
  }

  function sig(obj) { if (peer) conn.invoke('SendSignal', peer, JSON.stringify(obj)); }

  function hangup() { if (peer) conn.invoke('HangUp', peer); endCall(); }

  function endCall(note) {
    if (pc) { pc.close(); pc = null; }
    if (localStream) { localStream.getTracks().forEach(t => t.stop()); localStream = null; }
    $('#localVideo').srcObject = null;
    $('#remoteVideo').srcObject = null;
    peer = ''; pending = '';
    $('#incoming').classList.add('hidden');
    show(false);
    if (note) console.log('[call]', note);
  }

  // ── Controles ──
  function toggleMic() {
    if (!localStream) return;
    const t = localStream.getAudioTracks()[0]; if (!t) return;
    t.enabled = !t.enabled;
    $('#btnMic').classList.toggle('off', !t.enabled);
  }
  function toggleCam() {
    if (!localStream) return;
    const t = localStream.getVideoTracks()[0]; if (!t) return;
    t.enabled = !t.enabled;
    $('#btnCam').classList.toggle('off', !t.enabled);
  }

  // ── UI (se inyecta sola) ──
  function buildUI() {
    const wrap = document.createElement('div');
    wrap.id = 'callOverlay';
    wrap.className = 'call-overlay hidden';
    wrap.innerHTML =
      '<div class="call-box">' +
      '  <div id="callTitle" class="call-title">Videollamada</div>' +
      '  <div class="videos">' +
      '    <video id="remoteVideo" class="remote" autoplay playsinline></video>' +
      '    <video id="localVideo" class="local" autoplay playsinline muted></video>' +
      '  </div>' +
      '  <div id="callStatus" class="call-status"></div>' +
      '  <div id="incoming" class="incoming hidden">' +
      '    <button id="btnAccept" class="btn-accept" type="button">Aceptar 📹</button>' +
      '    <button id="btnDecline" class="btn-decline" type="button">Rechazar</button>' +
      '  </div>' +
      '  <div class="call-controls">' +
      '    <button id="btnMic" type="button" title="Silenciar micrófono">🎤</button>' +
      '    <button id="btnCam" type="button" title="Apagar cámara">📹</button>' +
      '    <button id="btnHang" class="btn-hang" type="button" title="Colgar">📞</button>' +
      '  </div>' +
      '</div>';
    document.body.appendChild(wrap);
    $('#btnAccept').onclick = accept;
    $('#btnDecline').onclick = decline;
    $('#btnHang').onclick = hangup;
    $('#btnMic').onclick = toggleMic;
    $('#btnCam').onclick = toggleCam;
  }

  function show(v) { $('#callOverlay').classList.toggle('hidden', !v); }
  function setStatus(t) { $('#callStatus').textContent = t; }
})();
