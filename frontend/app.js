// ASP.NET Core Web API Base URL
// Supports https://localhost:7098 or http://localhost:5084
let API_BASE = 'https://localhost:7098/api';

// Format JavaScript Date to YYYY-MM-DDTHH:mm for datetime-local input
function formatLocalISO(date) {
    const tzOffset = date.getTimezoneOffset() * 60000;
    return new Date(date.getTime() - tzOffset).toISOString().slice(0, 16);
}

// XSS Sanitizer Helper
function escapeHtml(str) {
    if (str === null || str === undefined) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// Display Banner Alerts and scroll smoothly to top
function showAlert(message, isSuccess = true) {
    const alert = document.getElementById('alertBanner');
    alert.textContent = message;
    alert.className = `alert ${isSuccess ? 'alert-success' : 'alert-error'}`;
    alert.style.display = 'block';
    
    // Scroll smoothly to top when creating or interacting
    window.scrollTo({ top: 0, behavior: 'smooth' });
    
    setTimeout(() => { alert.style.display = 'none'; }, 6000);
}

// Universal API Fetch Helper with HTTPS/HTTP Fallback
async function apiFetch(endpoint, options = {}) {
    const defaultHeaders = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
    options.headers = { ...defaultHeaders, ...options.headers };

    try {
        let response = await fetch(`${API_BASE}${endpoint}`, options);
        return response;
    } catch (err) {
        // Fallback to http://localhost:5084 if https://localhost:7098 is unreachable
        if (API_BASE.startsWith('https://localhost:7098')) {
            API_BASE = 'http://localhost:5084/api';
            return await fetch(`${API_BASE}${endpoint}`, options);
        }
        throw err;
    }
}

// -------------------------------------------------------------
// 1. Users Operations
// -------------------------------------------------------------
async function loadUsers() {
    const listEl = document.getElementById('usersList');
    try {
        const response = await apiFetch('/Users');
        if (response.ok) {
            const users = await response.json();
            if (!users || users.length === 0) {
                listEl.innerHTML = '<tr><td colspan="3" class="text-muted">No users found.</td></tr>';
                return;
            }
            listEl.innerHTML = users.map(u => `
                <tr>
                    <td><strong>${escapeHtml(u.id)}</strong></td>
                    <td>${escapeHtml(u.name)}</td>
                    <td>${escapeHtml(u.email)}</td>
                </tr>
            `).join('');
        } else {
            listEl.innerHTML = `<tr><td colspan="3" class="text-muted">Failed to load users (${response.status})</td></tr>`;
        }
    } catch (err) {
        listEl.innerHTML = '<tr><td colspan="3" class="text-muted">Cannot connect to API backend.</td></tr>';
    }
}

document.getElementById('createUserForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    window.scrollTo({ top: 0, behavior: 'smooth' });
    const name = document.getElementById('userName').value.trim();
    const email = document.getElementById('userEmail').value.trim();

    try {
        const response = await apiFetch('/Users', {
            method: 'POST',
            body: JSON.stringify({ name, email })
        });
        const data = await response.json();

        if (response.ok) {
            showAlert(`User "${data.name}" created successfully with ID: ${data.id}`, true);
            document.getElementById('createUserForm').reset();
            loadUsers();
        } else {
            showAlert(`Failed to create user (${response.status}): ${data.message || 'Error occurred'}`, false);
        }
    } catch (err) {
        showAlert('Network Error: Make sure ASP.NET Core API is running.', false);
    }
});

// -------------------------------------------------------------
// 2. Resources Operations
// -------------------------------------------------------------
async function loadResources() {
    const listEl = document.getElementById('resourcesList');
    try {
        const response = await apiFetch('/Resources');
        if (response.ok) {
            const resources = await response.json();
            if (!resources || resources.length === 0) {
                listEl.innerHTML = '<tr><td colspan="3" class="text-muted">No resources found.</td></tr>';
                return;
            }
            listEl.innerHTML = resources.map(r => `
                <tr>
                    <td><strong>${escapeHtml(r.id)}</strong></td>
                    <td>${escapeHtml(r.name)}</td>
                    <td>${escapeHtml(r.type)}</td>
                </tr>
            `).join('');
        } else {
            listEl.innerHTML = `<tr><td colspan="3" class="text-muted">Failed to load resources (${response.status})</td></tr>`;
        }
    } catch (err) {
        listEl.innerHTML = '<tr><td colspan="3" class="text-muted">Cannot connect to API backend.</td></tr>';
    }
}

document.getElementById('createResourceForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    window.scrollTo({ top: 0, behavior: 'smooth' });
    const name = document.getElementById('resourceName').value.trim();
    const type = document.getElementById('resourceType').value.trim();

    try {
        const response = await apiFetch('/Resources', {
            method: 'POST',
            body: JSON.stringify({ name, type })
        });
        const data = await response.json();

        if (response.ok) {
            showAlert(`Resource "${data.name}" created successfully with ID: ${data.id}`, true);
            document.getElementById('createResourceForm').reset();
            loadResources();
        } else {
            showAlert(`Failed to create resource (${response.status}): ${data.message || 'Error occurred'}`, false);
        }
    } catch (err) {
        showAlert('Network Error: Make sure ASP.NET Core API is running.', false);
    }
});

// -------------------------------------------------------------
// 3. Create Booking Operation
// -------------------------------------------------------------
document.getElementById('createBookingForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    window.scrollTo({ top: 0, behavior: 'smooth' });
    const resourceId = document.getElementById('bookingResourceId').value.trim();
    const userId = document.getElementById('bookingUserId').value.trim();
    const startDateTime = new Date(document.getElementById('bookingStart').value).toISOString();
    const endDateTime = new Date(document.getElementById('bookingEnd').value).toISOString();

    const payload = { resourceId, userId, startDateTime, endDateTime };

    try {
        const response = await apiFetch('/Bookings', {
            method: 'POST',
            body: JSON.stringify(payload)
        });

        const data = await response.json();

        if (response.ok) {
            showAlert(`Booking #${data.id} created successfully! (Status: ${data.status})`, true);
            document.getElementById('filterBookingsForm').dispatchEvent(new Event('submit'));
        } else if (response.status === 409) {
            showAlert(`Resource is already booked during this time.`, false);
        } else {
            showAlert(`Booking Error (${response.status}): ${data.message || 'Failed to create booking'}`, false);
        }
    } catch (err) {
        showAlert('Network Error: Make sure ASP.NET Core API is running.', false);
    }
});

// -------------------------------------------------------------
// 4. View & Filter Bookings Operations
// -------------------------------------------------------------
document.getElementById('filterBookingsForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const resourceId = document.getElementById('filterResourceId').value.trim();
    const fromVal = document.getElementById('filterFrom').value;
    const toVal = document.getElementById('filterTo').value;

    if (!resourceId) {
        showAlert('Please provide a Resource ID to filter bookings.', false);
        return;
    }

    const from = new Date(fromVal).toISOString();
    const to = new Date(toVal).toISOString();

    const listEl = document.getElementById('bookingsList');

    try {
        const response = await apiFetch(`/Bookings?resourceId=${encodeURIComponent(resourceId)}&from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`);
        const bookings = await response.json();

        if (response.ok) {
            if (!bookings || bookings.length === 0) {
                listEl.innerHTML = '<tr><td colspan="8" class="text-muted">No bookings found for the selected resource and date range.</td></tr>';
                return;
            }

            listEl.innerHTML = bookings.map(b => {
                const isCancelled = b.status === 'Cancelled';
                const statusBadge = isCancelled 
                    ? '<span class="badge badge-cancelled">Cancelled</span>'
                    : '<span class="badge badge-active">Active</span>';

                const sanitizedId = parseInt(b.id, 10);
                const actionButton = !isCancelled
                    ? `<button class="btn btn-danger" onclick="cancelBooking(${sanitizedId})">Cancel</button>`
                    : '—';

                return `
                    <tr>
                        <td><strong>#${sanitizedId}</strong></td>
                        <td>${escapeHtml(b.resourceId)}</td>
                        <td>${escapeHtml(b.userId)}</td>
                        <td>${new Date(b.startDateTime).toLocaleString()}</td>
                        <td>${new Date(b.endDateTime).toLocaleString()}</td>
                        <td>${statusBadge}</td>
                        <td>${new Date(b.createdAt).toLocaleString()}</td>
                        <td>${actionButton}</td>
                    </tr>
                `;
            }).join('');
        } else {
            listEl.innerHTML = `<tr><td colspan="8" class="text-muted">Error (${response.status}): ${escapeHtml(bookings.message || 'Failed to fetch bookings')}</td></tr>`;
        }
    } catch (err) {
        listEl.innerHTML = '<tr><td colspan="8" class="text-muted">Cannot connect to API backend.</td></tr>';
    }
});

// Cancel Booking
async function cancelBooking(id) {
    const sanitizedId = parseInt(id, 10);
    if (isNaN(sanitizedId)) return;

    if (!confirm(`Are you sure you want to cancel booking #${sanitizedId}?`)) return;

    try {
        const response = await apiFetch(`/Bookings/${sanitizedId}`, { method: 'DELETE' });

        window.scrollTo({ top: 0, behavior: 'smooth' });

        if (response.ok) {
            showAlert("Booking cancelled successfully.", true);
            document.getElementById('filterBookingsForm').dispatchEvent(new Event('submit'));
        } else {
            let errorMsg = 'Failed to cancel booking.';
            try { const data = await response.json(); errorMsg = data.message || errorMsg; } catch (_) {}
            showAlert(`Error (${response.status}): ${errorMsg}`, false);
        }
    } catch (err) {
        window.scrollTo({ top: 0, behavior: 'smooth' });
        showAlert('Network Error: Could not cancel booking.', false);
    }
}

// -------------------------------------------------------------
// Initialization on Page Load
// -------------------------------------------------------------
document.addEventListener('DOMContentLoaded', () => {
    const now = new Date();
    const twoHoursLater = new Date(now.getTime() + 2 * 3600000);
    const nextWeek = new Date(now.getTime() + 7 * 86400000);

    document.getElementById('bookingStart').value = formatLocalISO(now);
    document.getElementById('bookingEnd').value = formatLocalISO(twoHoursLater);
    document.getElementById('filterFrom').value = formatLocalISO(now);
    document.getElementById('filterTo').value = formatLocalISO(nextWeek);

    loadUsers();
    loadResources();
});
