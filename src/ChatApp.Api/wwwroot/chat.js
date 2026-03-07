const API_URL = '';
let token = localStorage.getItem('chat_token');
let currentUser = null;
let connection = null;
let activeRoomId = null;
let allUsers = [];      // all users (for name resolution)
let myConnects = [];    // accepted connections

// ── Avatar helpers ──────────────────────────────────────────────────────────
const avatarColors = [
    'avatar-color-0', 'avatar-color-1', 'avatar-color-2',
    'avatar-color-3', 'avatar-color-4', 'avatar-color-5'
];
function getAvatarColor(id) { return avatarColors[id % avatarColors.length]; }
function getInitial(name) { return (name || '?').charAt(0).toUpperCase(); }

// ── DOM refs ────────────────────────────────────────────────────────────────
const authContainer  = document.getElementById('auth-container');
const chatContainer  = document.getElementById('chat-container');
const loginForm      = document.getElementById('login-form');
const registerForm   = document.getElementById('register-form');
const authError      = document.getElementById('auth-error');
const tabLogin       = document.getElementById('tab-login');
const tabRegister    = document.getElementById('tab-register');
const tabIndicator   = document.querySelector('.tab-indicator');
const authTitle      = document.getElementById('auth-title-text');
const authSubtitle   = document.getElementById('auth-subtitle-text');
const connectsList   = document.getElementById('connects-list');
const roomsList      = document.getElementById('rooms-list');
const messagesContainer = document.getElementById('messages-container');
const messageForm    = document.getElementById('message-form');
const messageInput   = document.getElementById('message-input');
const chatWithTitle  = document.getElementById('chat-with-name');
const pendingSection = document.getElementById('pending-section');
const pendingBadge   = document.getElementById('pending-badge');

// ── Auth tab switching ──────────────────────────────────────────────────────
tabLogin.onclick = () => {
    tabLogin.classList.add('active');
    tabRegister.classList.remove('active');
    loginForm.classList.remove('hidden');
    registerForm.classList.add('hidden');
    tabIndicator.style.transform = 'translateX(0)';
    authTitle.textContent = 'Welcome back';
    authSubtitle.textContent = 'Sign in to continue your conversations';
};

tabRegister.onclick = () => {
    tabRegister.classList.add('active');
    tabLogin.classList.remove('active');
    registerForm.classList.remove('hidden');
    loginForm.classList.add('hidden');
    tabIndicator.style.transform = 'translateX(100%)';
    authTitle.textContent = 'Join Converso';
    authSubtitle.textContent = 'Create an account to start talking';
};

// ── Auth functions ──────────────────────────────────────────────────────────
async function login(email, password) {
    try {
        const res = await fetch(`${API_URL}/api/Auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || 'Login failed');
        token = data.token;
        localStorage.setItem('chat_token', token);
        initChat();
    } catch (err) {
        authError.textContent = err.message;
        authError.style.color = 'var(--error)';
    }
}

async function register(username, email, password) {
    try {
        const res = await fetch(`${API_URL}/api/Auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, email, password })
        });
        const data = await res.json();
        if (!res.ok) {
            let msg = data.message || 'Registration failed';
            if (data.errors) msg = Object.values(data.errors).flat().join(', ');
            throw new Error(msg);
        }
        tabLogin.click();
        authError.textContent = 'Account created! Sign in to get started.';
        authError.style.color = 'var(--success)';
        registerForm.reset();
    } catch (err) {
        authError.textContent = err.message;
        authError.style.color = 'var(--error)';
    }
}

// ── Chat init ───────────────────────────────────────────────────────────────
async function initChat() {
    try {
        const res = await fetch(`${API_URL}/api/Users/me`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!res.ok) throw new Error('Unauthorized');
        currentUser = await res.json();

        document.getElementById('current-username').textContent = currentUser.username;
        document.getElementById('current-username-initial').textContent = getInitial(currentUser.username);

        authContainer.classList.add('hidden');
        chatContainer.classList.remove('hidden');

        setupSignalR();
        await loadAllUsers();
        await Promise.all([loadConnects(), loadRooms(), loadPendingBadge()]);
    } catch {
        localStorage.removeItem('chat_token');
        authContainer.classList.remove('hidden');
        chatContainer.classList.add('hidden');
    }
}

// ── SignalR ─────────────────────────────────────────────────────────────────
function setupSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_URL}/chatHub`, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveMessage', (message) => {
        if (message.roomId === activeRoomId) {
            appendMessage(message);
            connection.invoke('MarkRoomAsRead', activeRoomId);
        } else {
            const roomEl = document.querySelector(`[data-room-id="${message.roomId}"]`);
            if (roomEl) roomEl.classList.add('has-unread');
        }
    });

    connection.on('UserPresenceUpdate', (userId, isOnline) => {
        document.querySelectorAll(`[data-user-id="${userId}"] .status-dot`).forEach(el => {
            el.className = `status-dot ${isOnline ? 'online' : 'offline'}`;
        });
    });

    connection.start()
        .then(() => console.log('SignalR connected'))
        .catch(err => console.error(err));
}

// ── Data loaders ────────────────────────────────────────────────────────────
async function loadAllUsers() {
    const res = await fetch(`${API_URL}/api/Users`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    if (res.ok) allUsers = await res.json();
}

async function loadConnects() {
    const res = await fetch(`${API_URL}/api/Connections`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    if (!res.ok) return;
    myConnects = await res.json();

    connectsList.innerHTML = '';
    if (myConnects.length === 0) {
        connectsList.innerHTML = '<div class="empty-section">No connects yet — add some people!</div>';
        return;
    }
    myConnects.forEach(u => connectsList.appendChild(buildUserItem(u)));
}

async function loadRooms() {
    const res = await fetch(`${API_URL}/api/ChatRooms`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    if (!res.ok) return;
    const rooms = await res.json();
    roomsList.innerHTML = '';
    rooms.forEach(room => addRoomToChannelsList(room));
}

async function loadPendingBadge() {
    const res = await fetch(`${API_URL}/api/Connections/pending`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    if (!res.ok) return;
    const { received } = await res.json();
    const count = received.length;
    if (count > 0) {
        pendingSection.style.display = '';
        pendingBadge.textContent = count;
    } else {
        pendingSection.style.display = 'none';
    }
}

// ── Build sidebar items ─────────────────────────────────────────────────────
function buildUserItem(u) {
    const item = document.createElement('div');
    item.className = 'user-item';
    item.dataset.userId = u.id;
    item.addEventListener('click', () => startPrivateChat(u.id, u.username));

    const avatar = document.createElement('div');
    avatar.className = `user-avatar ${getAvatarColor(u.id)}`;
    avatar.textContent = getInitial(u.username);

    const dot = document.createElement('span');
    dot.className = `status-dot ${u.isOnline ? 'online' : 'offline'}`;
    avatar.appendChild(dot);

    const info = document.createElement('div');
    info.className = 'user-item-info';
    const name = document.createElement('span');
    name.className = 'user-item-name';
    name.textContent = u.username;
    info.appendChild(name);

    item.appendChild(avatar);
    item.appendChild(info);
    return item;
}

function addRoomToChannelsList(room) {
    if (document.querySelector(`[data-room-id="${room.id}"]`)) return;

    let displayName = room.name;
    let otherUserId = null;

    if (room.type === 'Private' && room.participants) {
        const other = room.participants.find(p => p.userId !== currentUser.id);
        if (other) {
            otherUserId = other.userId;
            const u = allUsers.find(u => u.id === otherUserId);
            if (u) displayName = u.username;
        }
    }

    const item = document.createElement('div');
    item.className = 'room-item';
    item.dataset.roomId = room.id;

    const avatar = document.createElement('div');
    avatar.className = `user-avatar ${getAvatarColor(otherUserId ?? room.id)}`;
    avatar.textContent = room.type === 'Group' ? '#' : getInitial(displayName);

    const info = document.createElement('div');
    info.className = 'user-item-info';
    const nameEl = document.createElement('span');
    nameEl.className = 'user-item-name';
    nameEl.textContent = displayName;
    const typeEl = document.createElement('span');
    typeEl.className = 'room-item-type';
    typeEl.textContent = room.type === 'Group' ? 'Channel' : 'Direct';
    info.appendChild(nameEl);
    info.appendChild(typeEl);

    item.appendChild(avatar);
    item.appendChild(info);

    if (room.type === 'Private' && otherUserId !== null) {
        item.addEventListener('click', () => startPrivateChat(otherUserId, displayName));
    } else if (room.type === 'Group') {
        item.addEventListener('click', () => openRoom(room.id, displayName, null));
    }

    roomsList.appendChild(item);
}

// ── Chat opening ────────────────────────────────────────────────────────────
async function startPrivateChat(otherUserId, otherUsername) {
    try {
        const res = await fetch(`${API_URL}/api/ChatRooms/private/${otherUserId}`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        const room = await res.json();
        addRoomToChannelsList(room);
        await openRoom(room.id, otherUsername, otherUserId);
    } catch (err) {
        console.error('Failed to start chat', err);
    }
}

async function openRoom(roomId, displayName, otherUserId) {
    activeRoomId = roomId;

    document.getElementById('no-chat-selected').classList.add('hidden');
    document.getElementById('active-chat').classList.remove('hidden');
    chatWithTitle.textContent = displayName;

    const chatAvatar = document.getElementById('chat-avatar-initial');
    chatAvatar.textContent = otherUserId ? getInitial(displayName) : '#';
    chatAvatar.className = `chat-avatar ${getAvatarColor(otherUserId ?? roomId)}`;

    messagesContainer.innerHTML = '';

    const msgRes = await fetch(`${API_URL}/api/Messages/room/${roomId}`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    const messages = await msgRes.json();
    messages.reverse().forEach(appendMessage);

    if (connection.state === 'Connected') {
        await connection.invoke('JoinRoom', roomId);
        await connection.invoke('MarkRoomAsRead', roomId);
    }

    document.querySelectorAll('.user-item, .room-item').forEach(el => el.classList.remove('active'));
    if (otherUserId) {
        const userEl = document.querySelector(`[data-user-id="${otherUserId}"]`);
        if (userEl) { userEl.classList.add('active'); userEl.classList.remove('has-unread'); }
    }
    const roomEl = document.querySelector(`[data-room-id="${roomId}"]`);
    if (roomEl) { roomEl.classList.add('active'); roomEl.classList.remove('has-unread'); }
}

// ── Message rendering ───────────────────────────────────────────────────────
function appendMessage(msg) {
    const isSent = msg.senderId === currentUser.id;
    const el = document.createElement('div');
    el.className = `message ${isSent ? 'sent' : 'received'}`;
    const time = new Date(msg.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    const content = document.createElement('div');
    content.className = 'message-content';
    content.textContent = msg.content;

    const meta = document.createElement('span');
    meta.className = 'message-meta';
    meta.textContent = time;

    el.appendChild(content);
    el.appendChild(meta);
    messagesContainer.appendChild(el);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

// ── Modal helpers ───────────────────────────────────────────────────────────
function openModal(id) { document.getElementById(id).classList.remove('hidden'); }
function closeModal(id) { document.getElementById(id).classList.add('hidden'); }

document.querySelectorAll('.modal-close').forEach(btn => {
    btn.addEventListener('click', () => closeModal(btn.dataset.modal));
});
document.querySelectorAll('.modal-overlay').forEach(overlay => {
    overlay.addEventListener('click', (e) => {
        if (e.target === overlay) overlay.classList.add('hidden');
    });
});

// ── Add People modal ────────────────────────────────────────────────────────
document.getElementById('add-connect-btn').addEventListener('click', async () => {
    openModal('modal-add-people');
    await refreshDiscoverList();
});

document.getElementById('discover-search').addEventListener('input', (e) => {
    const q = e.target.value.toLowerCase();
    document.querySelectorAll('.discover-item').forEach(el => {
        el.style.display = el.dataset.name.includes(q) ? '' : 'none';
    });
});

async function refreshDiscoverList() {
    const list = document.getElementById('discover-list');
    list.innerHTML = '<div class="empty-section">Loading…</div>';

    const res = await fetch(`${API_URL}/api/Connections/discover`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    const users = res.ok ? await res.json() : [];
    list.innerHTML = '';

    if (users.length === 0) {
        list.innerHTML = '<div class="empty-section">No new people to discover.</div>';
        return;
    }

    users.forEach(u => {
        const row = document.createElement('div');
        row.className = 'discover-item';
        row.dataset.name = u.username.toLowerCase();

        const avatar = document.createElement('div');
        avatar.className = `user-avatar ${getAvatarColor(u.id)}`;
        avatar.textContent = getInitial(u.username);
        avatar.style.cssText = 'width:32px;height:32px;flex-shrink:0';

        const info = document.createElement('div');
        info.style.flex = '1';
        const name = document.createElement('div');
        name.className = 'discover-item-name';
        name.textContent = u.username;
        info.appendChild(name);

        const btn = document.createElement('button');
        btn.className = 'btn-sm btn-sm-primary';
        btn.textContent = 'Add';
        btn.addEventListener('click', async () => {
            btn.disabled = true;
            btn.textContent = 'Sending…';
            const r = await fetch(`${API_URL}/api/Connections/send/${u.id}`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (r.ok) {
                btn.textContent = 'Sent ✓';
            } else {
                const d = await r.json();
                btn.textContent = d.status === 'Accepted' ? 'Connected' : 'Pending';
            }
        });

        row.appendChild(avatar);
        row.appendChild(info);
        row.appendChild(btn);
        list.appendChild(row);
    });
}

// ── Pending Invites modal ───────────────────────────────────────────────────
document.getElementById('view-pending-btn').addEventListener('click', async () => {
    openModal('modal-pending');
    await refreshPendingModal();
});

document.querySelectorAll('.modal-tab').forEach(tab => {
    tab.addEventListener('click', () => {
        document.querySelectorAll('.modal-tab').forEach(t => t.classList.remove('active'));
        tab.classList.add('active');
        const which = tab.dataset.tab;
        document.getElementById('pending-received-list').classList.toggle('hidden', which !== 'received');
        document.getElementById('pending-sent-list').classList.toggle('hidden', which !== 'sent');
    });
});

async function refreshPendingModal() {
    const res = await fetch(`${API_URL}/api/Connections/pending`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    if (!res.ok) return;
    const { received, sent } = await res.json();

    const recList = document.getElementById('pending-received-list');
    const sentList = document.getElementById('pending-sent-list');
    recList.innerHTML = '';
    sentList.innerHTML = '';

    if (received.length === 0) {
        recList.innerHTML = '<div class="empty-section">No incoming requests.</div>';
    } else {
        received.forEach(r => {
            const row = document.createElement('div');
            row.className = 'pending-item';

            const avatar = document.createElement('div');
            avatar.className = `user-avatar ${getAvatarColor(r.senderId)}`;
            avatar.textContent = getInitial(r.senderUsername);
            avatar.style.cssText = 'width:32px;height:32px;flex-shrink:0';

            const info = document.createElement('div');
            info.style.flex = '1';
            const name = document.createElement('div');
            name.className = 'pending-item-name';
            name.textContent = r.senderUsername;
            info.appendChild(name);

            const acceptBtn = document.createElement('button');
            acceptBtn.className = 'btn-sm btn-sm-primary';
            acceptBtn.textContent = 'Accept';
            acceptBtn.style.marginRight = '0.375rem';
            acceptBtn.addEventListener('click', async () => {
                await fetch(`${API_URL}/api/Connections/${r.id}/accept`, {
                    method: 'PUT',
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                row.remove();
                await loadConnects();
                await loadPendingBadge();
            });

            const rejectBtn = document.createElement('button');
            rejectBtn.className = 'btn-sm btn-sm-danger';
            rejectBtn.textContent = 'Decline';
            rejectBtn.addEventListener('click', async () => {
                await fetch(`${API_URL}/api/Connections/${r.id}/reject`, {
                    method: 'PUT',
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                row.remove();
                await loadPendingBadge();
            });

            row.appendChild(avatar);
            row.appendChild(info);
            row.appendChild(acceptBtn);
            row.appendChild(rejectBtn);
            recList.appendChild(row);
        });
    }

    if (sent.length === 0) {
        sentList.innerHTML = '<div class="empty-section">No outgoing requests.</div>';
    } else {
        sent.forEach(r => {
            const row = document.createElement('div');
            row.className = 'pending-item';

            const avatar = document.createElement('div');
            avatar.className = `user-avatar ${getAvatarColor(r.receiverId)}`;
            avatar.textContent = getInitial(r.receiverUsername);
            avatar.style.cssText = 'width:32px;height:32px;flex-shrink:0';

            const info = document.createElement('div');
            info.style.flex = '1';
            const name = document.createElement('div');
            name.className = 'pending-item-name';
            name.textContent = r.receiverUsername;
            const sub = document.createElement('div');
            sub.className = 'pending-item-sub';
            sub.textContent = 'Awaiting response…';
            info.appendChild(name);
            info.appendChild(sub);

            row.appendChild(avatar);
            row.appendChild(info);
            sentList.appendChild(row);
        });
    }
}

// ── Create Channel modal ────────────────────────────────────────────────────
document.getElementById('create-channel-btn').addEventListener('click', async () => {
    document.getElementById('channel-name-input').value = '';
    document.getElementById('create-channel-error').textContent = '';
    await refreshMembersPicker();
    openModal('modal-create-channel');
});

async function refreshMembersPicker() {
    const container = document.getElementById('channel-members-list');
    container.innerHTML = '';

    if (myConnects.length === 0) {
        container.innerHTML = '<div class="empty-section" style="padding:0.5rem">Add some connects first.</div>';
        return;
    }

    myConnects.forEach(u => {
        const row = document.createElement('label');
        row.className = 'member-pick-item';

        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = u.id;

        const name = document.createElement('span');
        name.className = 'member-pick-name';
        name.textContent = u.username;

        row.appendChild(cb);
        row.appendChild(name);
        container.appendChild(row);
    });
}

document.getElementById('create-channel-submit').addEventListener('click', async () => {
    const name = document.getElementById('channel-name-input').value.trim();
    const errorEl = document.getElementById('create-channel-error');
    errorEl.textContent = '';

    if (!name) { errorEl.textContent = 'Channel name is required.'; return; }

    const checked = [...document.querySelectorAll('#channel-members-list input[type=checkbox]:checked')];
    if (checked.length === 0) { errorEl.textContent = 'Select at least one connect.'; return; }

    const memberIds = checked.map(cb => parseInt(cb.value));
    const res = await fetch(`${API_URL}/api/ChatRooms/group`, {
        method: 'POST',
        headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ name, memberIds })
    });

    if (!res.ok) {
        const d = await res.json();
        errorEl.textContent = d.message || 'Failed to create channel.';
        return;
    }

    const room = await res.json();
    addRoomToChannelsList(room);
    closeModal('modal-create-channel');
    await openRoom(room.id, room.name, null);
});

// ── Form handlers ───────────────────────────────────────────────────────────
loginForm.onsubmit = (e) => {
    e.preventDefault();
    login(document.getElementById('login-email').value, document.getElementById('login-password').value);
};

registerForm.onsubmit = (e) => {
    e.preventDefault();
    register(
        document.getElementById('reg-username').value,
        document.getElementById('reg-email').value,
        document.getElementById('reg-password').value
    );
};

messageForm.onsubmit = async (e) => {
    e.preventDefault();
    const content = messageInput.value.trim();
    if (!content || !activeRoomId) return;
    try {
        await connection.invoke('SendMessage', activeRoomId, content);
        messageInput.value = '';
    } catch (err) {
        console.error(err);
    }
};

document.getElementById('logout-btn').onclick = () => {
    localStorage.removeItem('chat_token');
    location.reload();
};

// ── Auto-init ───────────────────────────────────────────────────────────────
if (token) initChat();
