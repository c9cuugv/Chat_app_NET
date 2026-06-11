const sb = window.supabase.createClient(window.SUPABASE_URL, window.SUPABASE_ANON_KEY);

let currentUser = null;
let activeRoomId = null;
let allProfiles = [];
let myConnects = [];

// ── Avatar helpers ──────────────────────────────────────────────────────────
const avatarColors = [
    'avatar-color-0', 'avatar-color-1', 'avatar-color-2',
    'avatar-color-3', 'avatar-color-4', 'avatar-color-5'
];
function getAvatarColor(id) {
    if (!id) return avatarColors[0];
    let hash = 0;
    const str = String(id);
    for (let i = 0; i < str.length; i++) hash = str.charCodeAt(i) + ((hash << 5) - hash);
    return avatarColors[Math.abs(hash) % avatarColors.length];
}
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
        const { data, error } = await sb.auth.signInWithPassword({ email, password });
        if (error) throw new Error(error.message);
        currentUser = data.user;
        await initChat();
    } catch (err) {
        authError.textContent = err.message;
        authError.style.color = 'var(--error)';
    }
}

async function register(username, email, password) {
    try {
        const { data, error } = await sb.auth.signUp({
            email, password,
            options: { data: { username } }
        });
        if (error) throw new Error(error.message);
        if (data.session) {
            currentUser = data.user;
            await initChat();
        } else {
            tabLogin.click();
            authError.textContent = 'Account created! Check your email to confirm, then sign in.';
            authError.style.color = 'var(--success)';
            registerForm.reset();
        }
    } catch (err) {
        authError.textContent = err.message;
        authError.style.color = 'var(--error)';
    }
}

// ── Chat init ───────────────────────────────────────────────────────────────
async function initChat() {
    try {
        const { data: profile } = await supabase
            .from('profiles').select('*').eq('id', currentUser.id).single();

        currentUser.username = profile?.username || currentUser.email;
        document.getElementById('current-username').textContent = currentUser.username;
        document.getElementById('current-username-initial').textContent = getInitial(currentUser.username);

        authContainer.classList.add('hidden');
        chatContainer.classList.remove('hidden');

        setupPresence();
        setupRealtimeMessages();
        await loadAllProfiles();
        await Promise.all([loadConnects(), loadRooms(), loadPendingBadge()]);
    } catch (err) {
        console.error('initChat failed', err);
        await sb.auth.signOut();
        authContainer.classList.remove('hidden');
        chatContainer.classList.add('hidden');
    }
}

// ── Realtime ─────────────────────────────────────────────────────────────────
function setupPresence() {
    const ch = sb.channel('online-users');
    ch.on('presence', { event: 'sync' }, () => {
        const onlineIds = new Set(
            Object.values(ch.presenceState()).flat().map(p => p.user_id)
        );
        document.querySelectorAll('[data-user-id]').forEach(el => {
            const dot = el.querySelector('.status-dot');
            if (dot) dot.className = `status-dot ${onlineIds.has(el.dataset.userId) ? 'online' : 'offline'}`;
        });
    }).subscribe(async (status) => {
        if (status === 'SUBSCRIBED') await ch.track({ user_id: currentUser.id });
    });
}

function setupRealtimeMessages() {
    sb.channel('all-messages')
        .on('postgres_changes', { event: 'INSERT', schema: 'public', table: 'messages' }, (payload) => {
            const msg = payload.new;
            if (msg.room_id === activeRoomId) {
                appendMessage(normalizeMsg(msg));
                markRoomAsRead(msg.room_id);
            } else {
                const roomEl = document.querySelector(`[data-room-id="${msg.room_id}"]`);
                if (roomEl) roomEl.classList.add('has-unread');
            }
        })
        .subscribe();
}

// ── Data loaders ────────────────────────────────────────────────────────────
async function loadAllProfiles() {
    const { data } = await sb.from('profiles').select('*').neq('id', currentUser.id);
    allProfiles = data || [];
}

async function loadConnects() {
    const { data } = await supabase
        .from('connectionrequests')
        .select('*, sender:profiles!sender_id(id,username), receiver:profiles!receiver_id(id,username)')
        .eq('status', 'Accepted')
        .or(`sender_id.eq.${currentUser.id},receiver_id.eq.${currentUser.id}`);

    myConnects = (data || []).map(r =>
        r.sender_id === currentUser.id ? r.receiver : r.sender
    ).filter(Boolean);

    connectsList.innerHTML = '';
    if (myConnects.length === 0) {
        connectsList.innerHTML = '<div class="empty-section">No connects yet — add some people!</div>';
        return;
    }
    myConnects.forEach(u => connectsList.appendChild(buildUserItem(u)));
}

async function loadRooms() {
    const { data: parts } = await supabase
        .from('roomparticipants')
        .select('room_id, last_read_at, chatrooms(id,name,type)')
        .eq('user_id', currentUser.id);

    if (!parts) { roomsList.innerHTML = ''; return; }

    const privateIds = parts.filter(p => p.chatrooms?.type === 'Private').map(p => p.room_id);
    let otherMap = {};
    if (privateIds.length > 0) {
        const { data: others } = await supabase
            .from('roomparticipants')
            .select('room_id, user_id, profiles(id,username)')
            .in('room_id', privateIds)
            .neq('user_id', currentUser.id);
        (others || []).forEach(o => { otherMap[o.room_id] = { userId: o.user_id, username: o.profiles?.username }; });
    }

    roomsList.innerHTML = '';
    parts.forEach(p => {
        const room = p.chatrooms;
        if (!room) return;
        const other = room.type === 'Private' ? otherMap[room.id] : null;
        addRoomToChannelsList({
            id: room.id, name: room.name, type: room.type,
            _displayName: other?.username || room.name,
            _otherUserId: other?.userId || null
        });
    });
}

async function loadPendingBadge() {
    const { data } = await supabase
        .from('connectionrequests').select('id')
        .eq('receiver_id', currentUser.id).eq('status', 'Pending');
    const count = data?.length || 0;
    if (count > 0) { pendingSection.style.display = ''; pendingBadge.textContent = count; }
    else { pendingSection.style.display = 'none'; }
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
    dot.className = 'status-dot offline';
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

    const displayName = room._displayName || room.name;
    const otherUserId = room._otherUserId || null;

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

    if (room.type === 'Private' && otherUserId) {
        item.addEventListener('click', () => startPrivateChat(otherUserId, displayName));
    } else {
        item.addEventListener('click', () => openRoom(room.id, displayName, null));
    }

    roomsList.appendChild(item);
}

// ── Chat opening ────────────────────────────────────────────────────────────
async function startPrivateChat(otherUserId, otherUsername) {
    try {
        const dmKey = [currentUser.id, otherUserId].sort().join(':');
        const { data: existing } = await supabase
            .from('chatrooms').select('*').eq('type', 'Private').eq('name', dmKey).maybeSingle();

        if (existing) {
            addRoomToChannelsList({ ...existing, _displayName: otherUsername, _otherUserId: otherUserId });
            await openRoom(existing.id, otherUsername, otherUserId);
            return;
        }

        const { data: room } = await supabase
            .from('chatrooms').insert({ name: dmKey, type: 'Private' }).select().single();

        await sb.from('roomparticipants').insert([
            { room_id: room.id, user_id: currentUser.id },
            { room_id: room.id, user_id: otherUserId }
        ]);

        addRoomToChannelsList({ ...room, _displayName: otherUsername, _otherUserId: otherUserId });
        await openRoom(room.id, otherUsername, otherUserId);
    } catch (err) {
        console.error('startPrivateChat error', err);
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

    const { data: messages } = await supabase
        .from('messages').select('*').eq('room_id', roomId).order('sent_at', { ascending: true });

    (messages || []).forEach(msg => appendMessage(normalizeMsg(msg)));
    await markRoomAsRead(roomId);

    document.querySelectorAll('.user-item, .room-item').forEach(el => el.classList.remove('active'));
    if (otherUserId) {
        const userEl = document.querySelector(`[data-user-id="${otherUserId}"]`);
        if (userEl) { userEl.classList.add('active'); userEl.classList.remove('has-unread'); }
    }
    const roomEl = document.querySelector(`[data-room-id="${roomId}"]`);
    if (roomEl) { roomEl.classList.add('active'); roomEl.classList.remove('has-unread'); }
}

async function markRoomAsRead(roomId) {
    await sb.from('roomparticipants')
        .update({ last_read_at: new Date().toISOString() })
        .eq('room_id', roomId).eq('user_id', currentUser.id);
}

function normalizeMsg(raw) {
    return { senderId: raw.sender_id, content: raw.content, sentAt: raw.sent_at, roomId: raw.room_id };
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

    const { data: myConns } = await supabase
        .from('connectionrequests').select('sender_id, receiver_id')
        .or(`sender_id.eq.${currentUser.id},receiver_id.eq.${currentUser.id}`);

    const connectedIds = new Set(
        (myConns || []).flatMap(c => [c.sender_id, c.receiver_id]).filter(id => id !== currentUser.id)
    );
    const discoverable = allProfiles.filter(p => !connectedIds.has(p.id));

    list.innerHTML = '';
    if (discoverable.length === 0) {
        list.innerHTML = '<div class="empty-section">No new people to discover.</div>';
        return;
    }

    discoverable.forEach(u => {
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
            const { error } = await sb.from('connectionrequests').insert({
                sender_id: currentUser.id, receiver_id: u.id
            });
            btn.textContent = error ? 'Error' : 'Sent ✓';
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
    const { data } = await supabase
        .from('connectionrequests')
        .select('*, sender:profiles!sender_id(id,username), receiver:profiles!receiver_id(id,username)')
        .or(`sender_id.eq.${currentUser.id},receiver_id.eq.${currentUser.id}`)
        .eq('status', 'Pending');

    const received = (data || []).filter(r => r.receiver_id === currentUser.id);
    const sent = (data || []).filter(r => r.sender_id === currentUser.id);

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
            avatar.className = `user-avatar ${getAvatarColor(r.sender_id)}`;
            avatar.textContent = getInitial(r.sender?.username);
            avatar.style.cssText = 'width:32px;height:32px;flex-shrink:0';

            const info = document.createElement('div');
            info.style.flex = '1';
            const name = document.createElement('div');
            name.className = 'pending-item-name';
            name.textContent = r.sender?.username || 'Unknown';
            info.appendChild(name);

            const acceptBtn = document.createElement('button');
            acceptBtn.className = 'btn-sm btn-sm-primary';
            acceptBtn.textContent = 'Accept';
            acceptBtn.style.marginRight = '0.375rem';
            acceptBtn.addEventListener('click', async () => {
                await sb.from('connectionrequests').update({ status: 'Accepted' }).eq('id', r.id);
                row.remove();
                await loadConnects();
                await loadPendingBadge();
            });

            const rejectBtn = document.createElement('button');
            rejectBtn.className = 'btn-sm btn-sm-danger';
            rejectBtn.textContent = 'Decline';
            rejectBtn.addEventListener('click', async () => {
                await sb.from('connectionrequests').update({ status: 'Rejected' }).eq('id', r.id);
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
            avatar.className = `user-avatar ${getAvatarColor(r.receiver_id)}`;
            avatar.textContent = getInitial(r.receiver?.username);
            avatar.style.cssText = 'width:32px;height:32px;flex-shrink:0';

            const info = document.createElement('div');
            info.style.flex = '1';
            const name = document.createElement('div');
            name.className = 'pending-item-name';
            name.textContent = r.receiver?.username || 'Unknown';
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

    const memberIds = checked.map(cb => cb.value);

    const { data: room, error } = await supabase
        .from('chatrooms').insert({ name, type: 'Group' }).select().single();

    if (error) { errorEl.textContent = error.message; return; }

    await sb.from('roomparticipants').insert([
        { room_id: room.id, user_id: currentUser.id },
        ...memberIds.map(uid => ({ room_id: room.id, user_id: uid }))
    ]);

    addRoomToChannelsList({ ...room, _displayName: room.name, _otherUserId: null });
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
    const { error } = await sb.from('messages').insert({
        room_id: activeRoomId, sender_id: currentUser.id, content
    });
    if (!error) messageInput.value = '';
};

document.getElementById('logout-btn').onclick = async () => {
    await sb.auth.signOut();
    location.reload();
};

// ── Auto-init ───────────────────────────────────────────────────────────────
(async () => {
    const { data: { session } } = await sb.auth.getSession();
    if (session) {
        currentUser = session.user;
        await initChat();
    }
})();
