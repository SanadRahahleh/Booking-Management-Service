/**
 * BOOKING MANAGEMENT SERVICE — FRONTEND CONTROLLER
 * Pure Vanilla JavaScript (ES6) Dashboard Architecture
 * Compatible with ASP.NET Core 10 Web API
 */

// -------------------------------------------------------------
// 1. Configuration & Global State
// -------------------------------------------------------------
let API_BASE = 'https://localhost:7098/api';
const FALLBACK_API_BASE = 'http://localhost:5084/api';

const state = {
    users: [],
    resources: [],
    bookings: [],
    activeTab: 'tab-overview',
    pagination: {
        page: 1,
        pageSize: 10,
        hasMore: false
    },
    pendingCancelBookingId: null
};

// -------------------------------------------------------------
// 2. API Communication Layer
// -------------------------------------------------------------

/**
 * Universal fetch wrapper with automatic HTTPS -> HTTP fallback and centralized error handling
 */
async function apiFetch(endpoint, options = {}) {
    const defaultHeaders = {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
    };
    options.headers = { ...defaultHeaders, ...options.headers };

    try {
        const response = await fetch(`${API_BASE}${endpoint}`, options);
        return response;
    } catch (networkError) {
        // Automatic fallback from HTTPS (port 7098) to HTTP (port 5084)
        if (API_BASE.startsWith('https://localhost:7098')) {
            console.warn(`[API] HTTPS port unreachable, falling back to HTTP: ${FALLBACK_API_BASE}`);
            API_BASE = FALLBACK_API_BASE;
            updateApiStatusBadge(true, 'Connected (HTTP 5084)');
            return await fetch(`${API_BASE}${endpoint}`, options);
        }
        throw networkError;
    }
}

/**
 * Parses API error response and produces user-friendly message based on HTTP status code
 */
async function handleApiError(response, defaultMsg = 'Operation failed') {
    let serverMessage = '';
    try {
        const data = await response.json();
        serverMessage = data.message || '';
    } catch (_) {
        // Response was not JSON
    }

    switch (response.status) {
        case 400:
            return serverMessage ? `Validation Error: ${serverMessage}` : 'Please check the entered data.';
        case 404:
            return serverMessage || 'User, resource, or booking was not found.';
        case 409:
            return serverMessage || 'This resource is already booked during the selected time.';
        case 503:
            return 'The booking could not be completed because of a temporary concurrency conflict. Please try again.';
        case 500:
            return 'Something went wrong on the server. Please try again later.';
        default:
            return serverMessage || `${defaultMsg} (HTTP ${response.status})`;
    }
}

/**
 * Pings the API to update the status indicator in the topbar
 */
async function checkApiStatus() {
    const badge = document.getElementById('apiStatusBadge');
    const text = document.getElementById('apiStatusText');
    if (!badge || !text) return;

    badge.className = 'status-indicator status-checking';
    text.textContent = 'Checking...';

    try {
        const res = await apiFetch('/Resources');
        if (res.ok) {
            const isHttps = API_BASE.startsWith('https');
            updateApiStatusBadge(true, `API: Connected (${isHttps ? 'HTTPS 7098' : 'HTTP 5084'})`);
        } else {
            updateApiStatusBadge(false, `API Error (${res.status})`);
        }
    } catch (_) {
        updateApiStatusBadge(false, 'API: Disconnected');
    }
}

function updateApiStatusBadge(isConnected, label) {
    const badge = document.getElementById('apiStatusBadge');
    const text = document.getElementById('apiStatusText');
    if (!badge || !text) return;

    if (isConnected) {
        badge.className = 'status-indicator status-connected';
    } else {
        badge.className = 'status-indicator status-disconnected';
    }
    text.textContent = label;
}

// -------------------------------------------------------------
// 3. UI Helpers, Sanitization & Date Formatting
// -------------------------------------------------------------

/**
 * Robust XSS sanitization helper
 */
function escapeHtml(str) {
    if (str === null || str === undefined) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

/**
 * Format a JavaScript Date to YYYY-MM-DDTHH:mm for datetime-local inputs
 */
function toLocalIso(date) {
    const tzOffset = date.getTimezoneOffset() * 60000;
    return new Date(date.getTime() - tzOffset).toISOString().slice(0, 16);
}

/**
 * Format an ISO UTC string from API into local timezone human-readable string
 * e.g. "Oct 12, 2026, 10:30 AM"
 */
function formatDateTime(isoString) {
    if (!isoString) return '—';
    try {
        const d = new Date(isoString);
        if (isNaN(d.getTime())) return escapeHtml(isoString);
        return d.toLocaleString(undefined, {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    } catch (_) {
        return escapeHtml(isoString);
    }
}

/**
 * Displays floating toast notification
 */
function showToast(message, type = 'info', title = '') {
    const container = document.getElementById('toastContainer');
    if (!container) return;

    const icons = {
        success: '✓',
        error: '✕',
        warning: '⚠',
        info: 'ℹ'
    };

    const titles = {
        success: title || 'Success',
        error: title || 'Error',
        warning: title || 'Conflict Notice',
        info: title || 'Information'
    };

    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `
        <div class="toast-icon">${icons[type] || '•'}</div>
        <div class="toast-body">
            <div class="toast-title">${escapeHtml(titles[type])}</div>
            <div class="toast-message">${escapeHtml(message)}</div>
        </div>
        <button class="toast-close" aria-label="Dismiss">&times;</button>
    `;

    toast.querySelector('.toast-close').addEventListener('click', () => {
        dismissToast(toast);
    });

    container.appendChild(toast);

    setTimeout(() => {
        dismissToast(toast);
    }, 6500);
}

function dismissToast(toast) {
    if (!toast || !toast.parentNode) return;
    toast.classList.add('toast-hiding');
    setTimeout(() => {
        if (toast.parentNode) toast.parentNode.removeChild(toast);
    }, 250);
}

/**
 * Helper to manage button loading state
 */
function setButtonLoading(button, isLoading, loadingText = 'Processing...') {
    if (!button) return;
    if (isLoading) {
        button.dataset.originalHtml = button.innerHTML;
        button.disabled = true;
        button.innerHTML = `<span>${escapeHtml(loadingText)}</span>`;
    } else {
        button.disabled = false;
        if (button.dataset.originalHtml) {
            button.innerHTML = button.dataset.originalHtml;
        }
    }
}

// -------------------------------------------------------------
// 4. Tab Navigation Management
// -------------------------------------------------------------
function switchTab(tabId) {
    state.activeTab = tabId;

    // Update tab buttons
    document.querySelectorAll('.tab-btn').forEach(btn => {
        if (btn.dataset.tab === tabId) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });

    // Update tab panes
    document.querySelectorAll('.tab-pane').forEach(pane => {
        if (pane.id === tabId) {
            pane.classList.add('active');
        } else {
            pane.classList.remove('active');
        }
    });

    // Contextual refreshes
    if (tabId === 'tab-bookings') {
        const filterResource = document.getElementById('filterResourceId');
        if (filterResource && filterResource.value && state.bookings.length === 0) {
            loadBookings(1);
        }
    }
}
window.switchTab = switchTab;

// -------------------------------------------------------------
// 5. Users Operations
// -------------------------------------------------------------
async function loadUsers() {
    const tableBody = document.getElementById('usersTableBody');
    const badge = document.getElementById('userCountBadge');
    const statUsers = document.getElementById('statTotalUsers');

    if (tableBody) {
        tableBody.innerHTML = `<tr><td colspan="3" class="table-empty-state">Loading users...</td></tr>`;
    }

    try {
        const response = await apiFetch('/Users');
        if (response.ok) {
            const users = await response.json();
            state.users = Array.isArray(users) ? users : [];

            // Update Counts
            if (badge) badge.textContent = state.users.length;
            if (statUsers) statUsers.textContent = state.users.length;

            // Render Table
            if (tableBody) {
                if (state.users.length === 0) {
                    tableBody.innerHTML = `<tr><td colspan="3" class="table-empty-state">No users registered yet. Register a user on the left.</td></tr>`;
                } else {
                    tableBody.innerHTML = state.users.map(u => `
                        <tr>
                            <td><span class="id-badge">${escapeHtml(u.id)}</span></td>
                            <td><strong>${escapeHtml(u.name)}</strong></td>
                            <td>${escapeHtml(u.email)}</td>
                        </tr>
                    `).join('');
                }
            }

            // Update User Dropdown in Booking Form
            populateUserDropdown();

        } else {
            const errorMsg = await handleApiError(response, 'Failed to fetch users');
            if (tableBody) {
                tableBody.innerHTML = `<tr><td colspan="3" class="table-empty-state text-danger">${escapeHtml(errorMsg)}</td></tr>`;
            }
        }
    } catch (err) {
        if (tableBody) {
            tableBody.innerHTML = `<tr><td colspan="3" class="table-empty-state">Cannot connect to API backend.</td></tr>`;
        }
    }
}

function populateUserDropdown() {
    const userSelect = document.getElementById('bookingUserSelect');
    if (!userSelect) return;

    const currentVal = userSelect.value;
    userSelect.innerHTML = `<option value="" disabled ${!currentVal ? 'selected' : ''}>Choose a user...</option>`;

    state.users.forEach(u => {
        const opt = document.createElement('option');
        opt.value = u.id;
        opt.textContent = `${u.name} (${u.email})`;
        if (u.id === currentVal) opt.selected = true;
        userSelect.appendChild(opt);
    });
}

async function handleCreateUser(e) {
    e.preventDefault();
    const btn = document.getElementById('btnSubmitUser');
    const nameInput = document.getElementById('userName');
    const emailInput = document.getElementById('userEmail');

    const name = nameInput.value.trim();
    const email = emailInput.value.trim();

    if (!name || !email) {
        showToast('Name and Email are required.', 'error');
        return;
    }

    setButtonLoading(btn, true, 'Creating User...');

    try {
        const response = await apiFetch('/Users', {
            method: 'POST',
            body: JSON.stringify({ name, email })
        });

        if (response.ok) {
            const createdUser = await response.json();
            showToast(`User "${createdUser.name}" created successfully with ID: ${createdUser.id}`, 'success');
            document.getElementById('createUserForm').reset();
            await loadUsers();
        } else {
            const msg = await handleApiError(response, 'Failed to create user');
            showToast(msg, 'error');
        }
    } catch (err) {
        showToast('Network Error: Make sure ASP.NET Core API is running.', 'error');
    } finally {
        setButtonLoading(btn, false);
    }
}

// -------------------------------------------------------------
// 6. Resources Operations
// -------------------------------------------------------------
async function loadResources() {
    const tableBody = document.getElementById('resourcesTableBody');
    const badge = document.getElementById('resourceCountBadge');
    const statResources = document.getElementById('statTotalResources');

    if (tableBody) {
        tableBody.innerHTML = `<tr><td colspan="3" class="table-empty-state">Loading resources...</td></tr>`;
    }

    try {
        const response = await apiFetch('/Resources');
        if (response.ok) {
            const resources = await response.json();
            state.resources = Array.isArray(resources) ? resources : [];

            // Update Counts
            if (badge) badge.textContent = state.resources.length;
            if (statResources) statResources.textContent = state.resources.length;

            // Render Table
            if (tableBody) {
                if (state.resources.length === 0) {
                    tableBody.innerHTML = `<tr><td colspan="3" class="table-empty-state">No resources registered yet. Register a resource on the left.</td></tr>`;
                } else {
                    tableBody.innerHTML = state.resources.map(r => `
                        <tr>
                            <td><span class="id-badge">${escapeHtml(r.id)}</span></td>
                            <td><strong>${escapeHtml(r.name)}</strong></td>
                            <td><span class="badge" style="background:#f1f5f9;color:#334155;">${escapeHtml(r.type)}</span></td>
                        </tr>
                    `).join('');
                }
            }

            // Populate Resource Select Dropdowns
            populateResourceDropdowns();

        } else {
            const errorMsg = await handleApiError(response, 'Failed to fetch resources');
            if (tableBody) {
                tableBody.innerHTML = `<tr><td colspan="3" class="table-empty-state text-danger">${escapeHtml(errorMsg)}</td></tr>`;
            }
        }
    } catch (err) {
        if (tableBody) {
            tableBody.innerHTML = `<tr><td colspan="3" class="table-empty-state">Cannot connect to API backend.</td></tr>`;
        }
    }
}

function populateResourceDropdowns() {
    // 1. Booking Form Dropdown
    const bookingSelect = document.getElementById('bookingResourceSelect');
    if (bookingSelect) {
        const currentVal = bookingSelect.value;
        bookingSelect.innerHTML = `<option value="" disabled ${!currentVal ? 'selected' : ''}>Choose a resource...</option>`;
        state.resources.forEach(r => {
            const opt = document.createElement('option');
            opt.value = r.id;
            opt.textContent = `${r.name} (${r.type})`;
            if (r.id === currentVal) opt.selected = true;
            bookingSelect.appendChild(opt);
        });
    }

    // 2. Filter Form Dropdown
    const filterSelect = document.getElementById('filterResourceId');
    if (filterSelect) {
        const currentVal = filterSelect.value;
        filterSelect.innerHTML = `<option value="" disabled ${!currentVal ? 'selected' : ''}>Select a resource...</option>`;
        state.resources.forEach((r, idx) => {
            const opt = document.createElement('option');
            opt.value = r.id;
            opt.textContent = `${r.name} (${r.type})`;
            // Default select the first resource if none selected
            if (!currentVal && idx === 0) opt.selected = true;
            else if (r.id === currentVal) opt.selected = true;
            filterSelect.appendChild(opt);
        });
    }
}

async function handleCreateResource(e) {
    e.preventDefault();
    const btn = document.getElementById('btnSubmitResource');
    const idInput = document.getElementById('resourceIdInput');
    const nameInput = document.getElementById('resourceName');
    const typeInput = document.getElementById('resourceType');

    const id = idInput.value.trim();
    const name = nameInput.value.trim();
    const type = typeInput.value.trim();

    if (!name || !type) {
        showToast('Resource Name and Type are required.', 'error');
        return;
    }

    setButtonLoading(btn, true, 'Creating Resource...');

    const payload = { name, type };
    if (id) payload.id = id;

    try {
        const response = await apiFetch('/Resources', {
            method: 'POST',
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            const created = await response.json();
            showToast(`Resource "${created.name}" created successfully with ID: ${created.id}`, 'success');
            document.getElementById('createResourceForm').reset();
            await loadResources();
        } else {
            const msg = await handleApiError(response, 'Failed to create resource');
            showToast(msg, 'error');
        }
    } catch (err) {
        showToast('Network Error: Make sure ASP.NET Core API is running.', 'error');
    } finally {
        setButtonLoading(btn, false);
    }
}

// -------------------------------------------------------------
// 7. Booking Creation Operations
// -------------------------------------------------------------
async function handleCreateBooking(e) {
    e.preventDefault();
    const btn = document.getElementById('btnSubmitBooking');
    const resourceSelect = document.getElementById('bookingResourceSelect');
    const userSelect = document.getElementById('bookingUserSelect');
    const startInput = document.getElementById('bookingStart');
    const endInput = document.getElementById('bookingEnd');

    const resourceId = resourceSelect.value;
    const userId = userSelect.value;
    const startVal = startInput.value;
    const endVal = endInput.value;

    if (!resourceId) {
        showToast('Please select a Resource.', 'error');
        return;
    }
    if (!userId) {
        showToast('Please select a User.', 'error');
        return;
    }
    if (!startVal || !endVal) {
        showToast('Please provide both Start and End date/time.', 'error');
        return;
    }

    const startDate = new Date(startVal);
    const endDate = new Date(endVal);

    if (startDate >= endDate) {
        showToast('Start time must be strictly before end time.', 'error');
        return;
    }

    // Convert local datetime to UTC ISO string required by backend
    const payload = {
        resourceId,
        userId,
        startDateTime: startDate.toISOString(),
        endDateTime: endDate.toISOString()
    };

    setButtonLoading(btn, true, 'Verifying & Booking...');

    try {
        const response = await apiFetch('/Bookings', {
            method: 'POST',
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            const booking = await response.json();
            showToast(`Booking #${booking.id} created successfully! (Status: ${booking.status})`, 'success');
            
            // Sync filter resource dropdown and load bookings
            const filterResSelect = document.getElementById('filterResourceId');
            if (filterResSelect) filterResSelect.value = resourceId;

            // Switch to Bookings Explorer tab
            switchTab('tab-bookings');
            loadBookings(1);

        } else if (response.status === 409) {
            showToast('This resource is already booked during the selected time interval. Double booking was prevented by concurrency lock.', 'warning', 'Overlap Collision');
        } else if (response.status === 503) {
            showToast('The booking encountered transient database concurrency contention. Please retry your request.', 'warning', 'Concurrency Contention');
        } else {
            const msg = await handleApiError(response, 'Failed to create booking');
            showToast(msg, 'error');
        }
    } catch (err) {
        showToast('Network Error: Make sure ASP.NET Core API is running.', 'error');
    } finally {
        setButtonLoading(btn, false);
    }
}

// -------------------------------------------------------------
// 8. Bookings List, Filtering & Pagination
// -------------------------------------------------------------
async function loadBookings(page = 1) {
    const tableBody = document.getElementById('bookingsTableBody');
    const btnFilter = document.getElementById('btnFilterSubmit');
    const resourceSelect = document.getElementById('filterResourceId');
    const fromInput = document.getElementById('filterFrom');
    const toInput = document.getElementById('filterTo');
    const pageSizeSelect = document.getElementById('filterPageSize');
    const sortBySelect = document.getElementById('filterSortBy');
    const sortOrderSelect = document.getElementById('filterSortOrder');

    const resourceId = resourceSelect ? resourceSelect.value.trim() : '';
    const fromVal = fromInput ? fromInput.value : '';
    const toVal = toInput ? toInput.value : '';
    const pageSize = pageSizeSelect ? parseInt(pageSizeSelect.value, 10) : 10;
    const sortBy = sortBySelect ? sortBySelect.value : 'StartDateTime';
    const sortOrder = sortOrderSelect ? sortOrderSelect.value : 'asc';

    if (!resourceId) {
        showToast('Please select a Resource to filter bookings.', 'error');
        return;
    }

    if (!fromVal || !toVal) {
        showToast('Please provide both From and To dates.', 'error');
        return;
    }

    const fromDate = new Date(fromVal);
    const toDate = new Date(toVal);

    if (fromDate >= toDate) {
        showToast('From date must be strictly before To date.', 'error');
        return;
    }

    state.pagination.page = page;
    state.pagination.pageSize = pageSize;

    const fromIso = fromDate.toISOString();
    const toIso = toDate.toISOString();

    const queryUrl = `/Bookings?resourceId=${encodeURIComponent(resourceId)}&from=${encodeURIComponent(fromIso)}&to=${encodeURIComponent(toIso)}&page=${page}&pageSize=${pageSize}&sortBy=${encodeURIComponent(sortBy)}&sortOrder=${encodeURIComponent(sortOrder)}`;

    if (tableBody) {
        tableBody.innerHTML = `<tr><td colspan="8" class="table-empty-state">Loading bookings...</td></tr>`;
    }
    setButtonLoading(btnFilter, true, 'Searching...');

    try {
        const response = await apiFetch(queryUrl);

        if (response.ok) {
            const bookings = await response.json();
            state.bookings = Array.isArray(bookings) ? bookings : [];

            renderBookingsTable();
            updatePaginationControls();
            updateDashboardBookingStats();

        } else {
            const errorMsg = await handleApiError(response, 'Failed to fetch bookings');
            showToast(errorMsg, 'error');
            if (tableBody) {
                tableBody.innerHTML = `<tr><td colspan="8" class="table-empty-state text-danger">${escapeHtml(errorMsg)}</td></tr>`;
            }
        }
    } catch (err) {
        showToast('Network Error: Cannot connect to API backend.', 'error');
        if (tableBody) {
            tableBody.innerHTML = `<tr><td colspan="8" class="table-empty-state">Cannot connect to API backend.</td></tr>`;
        }
    } finally {
        setButtonLoading(btnFilter, false);
    }
}

function renderBookingsTable() {
    const tableBody = document.getElementById('bookingsTableBody');
    if (!tableBody) return;

    if (state.bookings.length === 0) {
        tableBody.innerHTML = `
            <tr>
                <td colspan="8" class="table-empty-state">
                    No bookings found for the selected resource and date range.
                </td>
            </tr>
        `;
        return;
    }

    tableBody.innerHTML = state.bookings.map(b => {
        const isCancelled = String(b.status).toLowerCase() === 'cancelled';
        const statusBadge = isCancelled
            ? '<span class="badge badge-cancelled">Cancelled</span>'
            : '<span class="badge badge-active">Active</span>';

        const actionCell = isCancelled
            ? '<span class="text-muted text-sm">—</span>'
            : `<button class="btn btn-danger btn-sm btn-cancel-booking" data-booking-id="${b.id}">Cancel</button>`;

        return `
            <tr>
                <td><span class="id-badge">#${b.id}</span></td>
                <td><strong>${escapeHtml(b.resourceId)}</strong></td>
                <td>${escapeHtml(b.userId)}</td>
                <td>${formatDateTime(b.startDateTime)}</td>
                <td>${formatDateTime(b.endDateTime)}</td>
                <td>${statusBadge}</td>
                <td>${formatDateTime(b.createdAt)}</td>
                <td class="text-right">${actionCell}</td>
            </tr>
        `;
    }).join('');

    // Attach event listeners to Cancel buttons via delegation
    tableBody.querySelectorAll('.btn-cancel-booking').forEach(btn => {
        btn.addEventListener('click', () => {
            const bookingId = parseInt(btn.dataset.bookingId, 10);
            openCancelConfirmModal(bookingId);
        });
    });
}

function updatePaginationControls() {
    const info = document.getElementById('paginationInfo');
    const badge = document.getElementById('pageIndicator');
    const btnPrev = document.getElementById('btnPrevPage');
    const btnNext = document.getElementById('btnNextPage');

    const count = state.bookings.length;
    const page = state.pagination.page;
    const pageSize = state.pagination.pageSize;

    if (info) {
        info.textContent = `Showing ${count} booking(s) on Page ${page}`;
    }
    if (badge) {
        badge.textContent = `Page ${page}`;
    }

    if (btnPrev) {
        btnPrev.disabled = page <= 1;
    }
    if (btnNext) {
        // If count is equal to pageSize, there are potentially more records
        btnNext.disabled = count < pageSize;
    }
}

function updateDashboardBookingStats() {
    const statActive = document.getElementById('statActiveBookings');
    const statCancelled = document.getElementById('statCancelledBookings');

    let activeCount = 0;
    let cancelledCount = 0;

    state.bookings.forEach(b => {
        if (String(b.status).toLowerCase() === 'cancelled') {
            cancelledCount++;
        } else {
            activeCount++;
        }
    });

    if (statActive) statActive.textContent = activeCount;
    if (statCancelled) statCancelled.textContent = cancelledCount;
}

// -------------------------------------------------------------
// 9. Booking Cancellation Modal Operations
// -------------------------------------------------------------
function openCancelConfirmModal(bookingId) {
    state.pendingCancelBookingId = bookingId;
    const modal = document.getElementById('confirmModal');
    const idText = document.getElementById('modalBookingIdText');

    if (idText) idText.textContent = `#${bookingId}`;
    if (modal) {
        modal.classList.add('active');
        modal.setAttribute('aria-hidden', 'false');
    }
}

function closeCancelConfirmModal() {
    state.pendingCancelBookingId = null;
    const modal = document.getElementById('confirmModal');
    if (modal) {
        modal.classList.remove('active');
        modal.setAttribute('aria-hidden', 'true');
    }
}

async function executeCancelBooking() {
    const bookingId = state.pendingCancelBookingId;
    if (!bookingId) return;

    const btn = document.getElementById('btnConfirmCancelBooking');
    setButtonLoading(btn, true, 'Cancelling...');

    try {
        const response = await apiFetch(`/Bookings/${bookingId}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            showToast(`Booking #${bookingId} has been soft-cancelled. The slot is now released.`, 'success');
            closeCancelConfirmModal();
            // Refresh bookings
            await loadBookings(state.pagination.page);
        } else {
            const errorMsg = await handleApiError(response, 'Failed to cancel booking');
            showToast(errorMsg, 'error');
            closeCancelConfirmModal();
        }
    } catch (err) {
        showToast('Network Error: Could not cancel booking.', 'error');
        closeCancelConfirmModal();
    } finally {
        setButtonLoading(btn, false);
    }
}

// -------------------------------------------------------------
// 10. Initialization & Event Listeners
// -------------------------------------------------------------
document.addEventListener('DOMContentLoaded', async () => {
    
    // Set initial date defaults
    const now = new Date();
    // Round to next 30 minutes
    now.setMinutes(Math.ceil(now.getMinutes() / 30) * 30, 0, 0);

    const twoHoursLater = new Date(now.getTime() + 2 * 3600000);
    const fourteenDaysLater = new Date(now.getTime() + 14 * 86400000);

    const bookingStart = document.getElementById('bookingStart');
    const bookingEnd = document.getElementById('bookingEnd');
    const filterFrom = document.getElementById('filterFrom');
    const filterTo = document.getElementById('filterTo');

    if (bookingStart) bookingStart.value = toLocalIso(now);
    if (bookingEnd) bookingEnd.value = toLocalIso(twoHoursLater);
    if (filterFrom) filterFrom.value = toLocalIso(now);
    if (filterTo) filterTo.value = toLocalIso(fourteenDaysLater);

    // Tab buttons event listeners
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            switchTab(btn.dataset.tab);
        });
    });

    // Forms
    const createUserForm = document.getElementById('createUserForm');
    if (createUserForm) createUserForm.addEventListener('submit', handleCreateUser);

    const createResourceForm = document.getElementById('createResourceForm');
    if (createResourceForm) createResourceForm.addEventListener('submit', handleCreateResource);

    const createBookingForm = document.getElementById('createBookingForm');
    if (createBookingForm) createBookingForm.addEventListener('submit', handleCreateBooking);

    const filterBookingsForm = document.getElementById('filterBookingsForm');
    if (filterBookingsForm) {
        filterBookingsForm.addEventListener('submit', (e) => {
            e.preventDefault();
            loadBookings(1);
        });
    }

    const btnResetFilters = document.getElementById('btnResetFilters');
    if (btnResetFilters) {
        btnResetFilters.addEventListener('click', () => {
            if (filterFrom) filterFrom.value = toLocalIso(now);
            if (filterTo) filterTo.value = toLocalIso(fourteenDaysLater);
            loadBookings(1);
        });
    }

    // Pagination buttons
    const btnPrev = document.getElementById('btnPrevPage');
    if (btnPrev) {
        btnPrev.addEventListener('click', () => {
            if (state.pagination.page > 1) {
                loadBookings(state.pagination.page - 1);
            }
        });
    }

    const btnNext = document.getElementById('btnNextPage');
    if (btnNext) {
        btnNext.addEventListener('click', () => {
            loadBookings(state.pagination.page + 1);
        });
    }

    // Refresh All button
    const btnRefreshAll = document.getElementById('btnRefreshAll');
    if (btnRefreshAll) {
        btnRefreshAll.addEventListener('click', async () => {
            setButtonLoading(btnRefreshAll, true, 'Refreshing...');
            await checkApiStatus();
            await loadResources();
            await loadUsers();
            const filterRes = document.getElementById('filterResourceId');
            if (filterRes && filterRes.value) {
                await loadBookings(1);
            }
            setButtonLoading(btnRefreshAll, false);
            showToast('All dashboard data refreshed.', 'info');
        });
    }

    // Modal listeners
    const btnModalClose = document.getElementById('btnModalClose');
    if (btnModalClose) btnModalClose.addEventListener('click', closeCancelConfirmModal);

    const btnCancelModal = document.getElementById('btnCancelModal');
    if (btnCancelModal) btnCancelModal.addEventListener('click', closeCancelConfirmModal);

    const btnConfirmCancel = document.getElementById('btnConfirmCancelBooking');
    if (btnConfirmCancel) btnConfirmCancel.addEventListener('click', executeCancelBooking);

    // Initial Load Sequence
    await checkApiStatus();
    await loadResources();
    await loadUsers();

    // If resources exist, auto-select first resource and load its bookings
    const filterRes = document.getElementById('filterResourceId');
    if (filterRes && filterRes.value) {
        await loadBookings(1);
    }
});
