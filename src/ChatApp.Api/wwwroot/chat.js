const API_URL = '';
let token = localStorage.getItem('chat_token');
let currentUser = null;
let connection = null;
let activeRoomId = null;

// Avatar color palette
const avatarColors = [
    'avatar-color-0', 'avatar-color-1', 'avatar-color-2',
    'avatar-color-3', 'avatar-color-4', 'avatar-color-5'
];

function getAvatarColor(id) {
    return avatarColors[id % avatarColors.length];
}

function getInitial(name) {
    return (name || '?').charAt(0).toUpperCase();
}

// DOM Elements
const authContainer = document.getElementById('auth-container');
const chatContainer = document.getElementById('chat-container');
const loginForm = document.getElementById('login-form');
const registerForm = document.getElementById('register-form');
const authError = document.getElementById('auth-error');
const tabLogin = document.getElementById('tab-login');
const tabRegister = document.getElementById('tab-register');
const tabIndicator = document.querySelector('.tab-indicator');
const authTitle = document.getElementById('auth-title-text');
const authSubtitle = document.getElementById('auth-subtitle-text');
const usersList = document.getElementById('users-list');
const messagesContainer = document.getElementById('messages-container');
const messageForm = document.getElementById('message-form');
const messageInput = document.getElementById('message-input');
const chatWithTitle = document.getElementById('chat-with-name');

// Tab Switching
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

// Auth Functions
async function login(email, password) {
    try {
        const response = await fetch(`${API_URL}/api/Auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        const data = await response.json();
        if (!response.ok) throw new Error(data.message || 'Login failed');

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
        const response = await fetch(`${API_URL}/api/Auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, email, password })
        });

        const data = await response.json();
        if (!response.ok) {
            let msg = data.message || 'Registration failed';
            if (data.errors) {
                msg = Object.values(data.errors).flat().join(', ');
            }
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

// Chat Initialization
async function initChat() {
    try {
        const response = await fetch(`${API_URL}/api/Users/me`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (!response.ok) throw new Error('Unauthorized');

        currentUser = await response.json();
        document.getElementById('current-username').textContent = currentUser.username;
        document.getElementById('current-username-initial').textContent = getInitial(currentUser.username);

        authContainer.classList.add('hidden');
        chatContainer.classList.remove('hidden');

        setupSignalR();
        loadUsers();
    } catch (err) {
        localStorage.removeItem('chat_token');
        authContainer.classList.remove('hidden');
        chatContainer.classList.add('hidden');
    }
}

function setupSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_URL}/chatHub`, {
            accessTokenFactory: () => token
        })
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMessage", (message) => {
        if (message.roomId === activeRoomId) {
            appendMessage(message);
            connection.invoke("MarkRoomAsRead", activeRoomId);
        } else {
            const userItem = document.querySelector(`[data-user-id="${message.senderId}"]`);
            if (userItem) {
                userItem.classList.add('has-unread');
                usersList.prepend(userItem);
            }
        }
    });

    connection.on("UserPresenceUpdate", (userId, isOnline) => {
        const userEl = document.querySelector(`[data-user-id="${userId}"] .status-dot`);
        if (userEl) {
            userEl.className = `status-dot ${isOnline ? 'online' : 'offline'}`;
        }
    });

    connection.start()
        .then(() => console.log("SignalR Connected"))
        .catch(err => console.error(err));
}

async function loadUsers() {
    const response = await fetch(`${API_URL}/api/Users`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    const users = await response.json();

    usersList.innerHTML = '';
    users
        .filter(u => u.id !== currentUser.id)
        .forEach(u => {
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
            usersList.appendChild(item);
        });
}

async function startPrivateChat(otherUserId, otherUsername) {
    try {
        const response = await fetch(`${API_URL}/api/ChatRooms/private/${otherUserId}`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        const room = await response.json();

        activeRoomId = room.id;

        document.getElementById('no-chat-selected').classList.add('hidden');
        document.getElementById('active-chat').classList.remove('hidden');
        chatWithTitle.textContent = otherUsername;

        // Update chat avatar
        const chatAvatar = document.getElementById('chat-avatar-initial');
        chatAvatar.textContent = getInitial(otherUsername);
        chatAvatar.className = `chat-avatar ${getAvatarColor(otherUserId)}`;

        messagesContainer.innerHTML = '';

        // Load history
        const messagesRes = await fetch(`${API_URL}/api/Messages/room/${activeRoomId}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        const messages = await messagesRes.json();

        messages.reverse().forEach(appendMessage);

        // Join the room in SignalR
        await connection.invoke("JoinRoom", activeRoomId);
        await connection.invoke("MarkRoomAsRead", activeRoomId);

        // Highlight active user
        document.querySelectorAll('.user-item').forEach(el => el.classList.remove('active'));
        const activeUserEl = document.querySelector(`[data-user-id="${otherUserId}"]`);
        if (activeUserEl) {
            activeUserEl.classList.add('active');
            activeUserEl.classList.remove('has-unread');
        }

    } catch (err) {
        console.error("Failed to start chat", err);
    }
}

function appendMessage(msg) {
    const isSent = msg.senderId === currentUser.id;
    const msgEl = document.createElement('div');
    msgEl.className = `message ${isSent ? 'sent' : 'received'}`;
    const time = new Date(msg.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    const contentEl = document.createElement('div');
    contentEl.className = 'message-content';
    contentEl.textContent = msg.content; // textContent prevents XSS

    const metaEl = document.createElement('span');
    metaEl.className = 'message-meta';
    metaEl.textContent = time;

    msgEl.appendChild(contentEl);
    msgEl.appendChild(metaEl);
    messagesContainer.appendChild(msgEl);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

// Form Handlers
loginForm.onsubmit = (e) => {
    e.preventDefault();
    login(document.getElementById('login-email').value, document.getElementById('login-password').value);
};

registerForm.onsubmit = (e) => {
    e.preventDefault();
    register(document.getElementById('reg-username').value,
        document.getElementById('reg-email').value,
        document.getElementById('reg-password').value);
};

messageForm.onsubmit = async (e) => {
    e.preventDefault();
    const content = messageInput.value.trim();
    if (!content || !activeRoomId) return;

    try {
        await connection.invoke("SendMessage", activeRoomId, content);
        messageInput.value = '';
    } catch (err) {
        console.error(err);
    }
};

document.getElementById('logout-btn').onclick = () => {
    localStorage.removeItem('chat_token');
    location.reload();
};

// Auto-init
if (token) initChat();
