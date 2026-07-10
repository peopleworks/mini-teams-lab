const $ = s => document.querySelector(s);
let me = "";
let conn = null;

$('#enter').addEventListener('click', start);
$('#name').addEventListener('keydown', e => { if (e.key === 'Enter') start(); });

async function start() {
  me = ($('#name').value || '').trim();
  if (!me) return;
  $('#login').classList.add('hidden');
  $('#app').classList.remove('hidden');

  conn = new signalR.HubConnectionBuilder()
    .withUrl(`/hub/chat?user=${encodeURIComponent(me)}`)
    .withAutomaticReconnect()
    .build();

  conn.on('ReceiveMessage', (user, text, sentAt) => addMessage(user, text, sentAt));
  conn.on('PresenceChanged', users => renderUsers(users));
  conn.on('UserTyping', (user, isTyping) => showTyping(user, isTyping));
  conn.on('ReceiveReaction', (user, emoji) => onReaction(user, emoji));

  if (window.initCall) window.initCall(conn, me);   // módulo de videollamada (opcional)

  await conn.start();
  $('#text').focus();
}

$('#form').addEventListener('submit', async e => {
  e.preventDefault();
  const t = $('#text').value.trim();
  if (!t || !conn) return;
  await conn.invoke('SendMessage', t);
  $('#text').value = '';
  await conn.invoke('SetTyping', false);
});

let typingTimer;
$('#text').addEventListener('input', async () => {
  if (!conn) return;
  await conn.invoke('SetTyping', true);
  clearTimeout(typingTimer);
  typingTimer = setTimeout(() => conn && conn.invoke('SetTyping', false), 1500);
});

// ── Reacciones en vivo ── (misma idea del contrato: un toque → TODOS lo ven)
document.querySelectorAll('.react').forEach(btn => {
  btn.addEventListener('click', () => {
    if (!conn) return;
    conn.invoke('SendReaction', btn.dataset.emoji);
  });
});

function onReaction(user, emoji) {
  floatEmoji(emoji);
  bumpApplause();
}

// Un emoji que sube flotando por la pantalla, con posición y deriva al azar.
function floatEmoji(emoji) {
  const el = document.createElement('div');
  el.className = 'fx-emoji';
  el.textContent = emoji;                                 // textContent = sin riesgo de HTML
  el.style.left = (8 + Math.random() * 84) + 'vw';
  el.style.setProperty('--dx', (Math.random() * 120 - 60) + 'px');
  el.style.fontSize = (1.6 + Math.random() * 1.2) + 'rem';
  el.style.animationDuration = (2.2 + Math.random() * 1.2) + 's';
  const fx = $('#fx');
  fx.appendChild(el);
  el.addEventListener('animationend', () => el.remove());
}

// Aplausómetro: sube con cada reacción y baja solo. Al llegar al tope, ¡confeti para rematar!
let applause = 0, cooling = false;
function bumpApplause() {
  applause = Math.min(100, applause + 9);
  updateMeter();
  if (applause >= 100 && !cooling) {
    cooling = true;
    confettiBurst();
    setTimeout(() => { cooling = false; }, 2500);
  }
}
setInterval(() => {
  if (applause > 0) { applause = Math.max(0, applause - 3); updateMeter(); }
}, 250);
function updateMeter() {
  const f = $('#meterFill');
  if (f) f.style.width = applause + '%';
}

// Estallido de confeti (puro CSS animado; se limpia solo).
function confettiBurst() {
  const fx = $('#fx');
  const colors = ['#7c3aed', '#0F766E', '#ff9d10', '#ec4899', '#22c55e', '#3b82f6'];
  for (let i = 0; i < 90; i++) {
    const p = document.createElement('div');
    p.className = 'confetti';
    p.style.left = Math.random() * 100 + 'vw';
    p.style.background = colors[i % colors.length];
    p.style.setProperty('--dx', (Math.random() * 260 - 130) + 'px');
    p.style.animationDuration = (1.8 + Math.random() * 1.4) + 's';
    p.style.animationDelay = (Math.random() * 0.25) + 's';
    fx.appendChild(p);
    p.addEventListener('animationend', () => p.remove());
  }
}

function addMessage(user, text, sentAt) {
  const mine = user === me;
  const el = document.createElement('div');
  el.className = 'msg' + (mine ? ' mine' : '');
  const time = new Date(sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  el.innerHTML =
    `<div class="who">${mine ? 'Tú' : escapeHtml(user)}</div>` +
    `<div class="bubble">${escapeHtml(text)}</div>` +
    `<div class="time">${time}</div>`;
  const box = $('#messages');
  box.appendChild(el);
  box.scrollTop = box.scrollHeight;
}

const typingSet = new Set();
function showTyping(user, isTyping) {
  if (user === me) return;
  if (isTyping) typingSet.add(user); else typingSet.delete(user);
  const arr = [...typingSet];
  $('#typing').textContent = arr.length
    ? `${arr.join(', ')} ${arr.length > 1 ? 'están' : 'está'} escribiendo…`
    : '';
}

function renderUsers(users) {
  $('#count').textContent = `· ${users.length}`;
  $('#users').innerHTML = users.map(u => {
    const call = u === me ? '' : `<button class="call-btn" data-user="${escapeHtml(u)}" title="Llamar a ${escapeHtml(u)}" type="button">📞</button>`;
    return `<li><span class="dot"></span><span class="uname">${escapeHtml(u)}</span>${call}</li>`;
  }).join('');
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}
