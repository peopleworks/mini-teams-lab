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
  $('#users').innerHTML = users.map(u => `<li><span class="dot"></span>${escapeHtml(u)}</li>`).join('');
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}
