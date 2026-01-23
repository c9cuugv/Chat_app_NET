const API_URL = '';
let token = localStorage.getItem('chat_token');
let currentUser = null;
let connection = null;
let activeRoomId = null;

// DOM Elements
const authContainer = document.getElementById('auth-container');
const chatContainer = document.getElementById('chat-container');
const loginForm = document.getElementById('login-form');
const registerForm = document.getElementById('register-form');
const authError = document.getElementById('auth-error');
const tabLogin = document.getElementById('tab-login');
const tabRegister = document.getElementById('tab-register');
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
};

tabRegister.onclick = () => {
    tabRegister.classList.add('active');
    tabLogin.classList.remove('active');
    registerForm.classList.remove('hidden');
    loginForm.classList.add('hidden');
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
        authError.innerText = err.message;
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
            // Check for validation errors (ModelState)
            let msg = data.message || 'Registration failed';
            if (data.errors) {
                msg = Object.values(data.errors).flat().join(', ');
            }
            throw new Error(msg);
        }

        tabLogin.click();
        authError.innerText = 'Registration successful! Please login.';
        authError.style.color = 'var(--success)';
        // Clear registration form
        registerForm.reset();
    } catch (err) {
        authError.innerText = err.message;
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
        document.getElementById('current-username').innerText = currentUser.username;

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
        console.log(`Recv: ${message.roomId}, Active: ${activeRoomId}`);
        if (message.roomId === activeRoomId) {
            appendMessage(message);
            connection.invoke("MarkRoomAsRead", activeRoomId);
        } else {
            // Highlight user in list if not active
            const userItem = document.querySelector(`[data-user-id="${message.senderId}"]`);
            if (userItem) {
                userItem.classList.add('has-unread');
                // Optionally move to top
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

    usersList.innerHTML = users
        .filter(u => u.id !== currentUser.id)
        .map(u => `
            <div class="user-item" onclick="startPrivateChat(${u.id}, '${u.username}')" data-user-id="${u.id}">
                <div class="user-info">
                    <span class="status-dot ${u.isOnline ? 'online' : 'offline'}"></span>
                    <span>${u.username}</span>
                </div>
            </div>
        `).join('');
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
        chatWithTitle.innerText = otherUsername;
        messagesContainer.innerHTML = '';

        // Load history
        const messagesRes = await fetch(`${API_URL}/api/Messages/room/${activeRoomId}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        const messages = await messagesRes.json();

        messages.reverse().forEach(appendMessage); // API returns recent first, we want chronological

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
    msgEl.innerHTML = `
        <div class="message-content">${msg.content}</div>
        <span class="message-meta">${time}</span>
    `;
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
