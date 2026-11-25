const state = {
  events: [],
  selectedEvent: null,
  trackedBookingId: null
};

const eventGrid = document.getElementById("event-grid");
const eventsLoading = document.getElementById("events-loading");
const bookingList = document.getElementById("booking-list");
const bookingsLoading = document.getElementById("bookings-loading");
const bookingDialog = document.getElementById("booking-dialog");

function escapeHtml(value) {
  const element = document.createElement("span");
  element.textContent = value ?? "";
  return element.innerHTML;
}

function formatDate(value) {
  return new Intl.DateTimeFormat("vi-VN", {
    weekday: "short",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}

function statusText(status) {
  const value = String(status).toLowerCase();
  if (value === "confirmed" || value === "1") return "Đã xác nhận";
  if (value === "rejected" || value === "2") return "Bị từ chối";
  return "Đang xử lý";
}

function statusClass(status) {
  const value = String(status).toLowerCase();
  if (value === "confirmed" || value === "1") return "confirmed";
  if (value === "rejected" || value === "2") return "rejected";
  return "pending";
}

function showToast(message, type = "success") {
  const toast = document.createElement("div");
  toast.className = `toast ${type}`;
  toast.textContent = message;
  document.getElementById("toast-region").appendChild(toast);
  window.setTimeout(() => toast.remove(), 4000);
}

async function apiFetch(url, options) {
  const response = await fetch(url, options);
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(data?.error || `Yêu cầu thất bại (${response.status})`);
  }
  return data;
}

async function loadEvents() {
  eventsLoading.classList.remove("hidden");
  eventGrid.innerHTML = "";
  try {
    state.events = await apiFetch("/events");
    renderEvents(state.events);
  } catch (error) {
    eventGrid.innerHTML = "";
    eventsLoading.classList.remove("hidden");
    eventsLoading.innerHTML = `<p>Không thể tải sự kiện: ${escapeHtml(error.message)}</p>`;
  } finally {
    eventsLoading.classList.add("hidden");
  }
}

function renderEvents(events) {
  if (!events.length) {
    eventGrid.innerHTML = '<div class="state-card"><p>Không tìm thấy sự kiện phù hợp.</p></div>';
    return;
  }

  eventGrid.innerHTML = events.map((event, index) => {
    const date = new Date(event.startsAt);
    const sold = event.capacity - event.availableTickets;
    const soldPercent = event.capacity ? Math.max(0, Math.min(100, sold / event.capacity * 100)) : 100;
    const soldOut = event.availableTickets <= 0;
    return `
      <article class="event-card">
        <div class="event-cover ${index % 2 ? "music" : ""}">
          <div class="event-date">
            <small>${date.toLocaleDateString("vi-VN", { month: "short" }).toUpperCase()}</small>
            <strong>${date.getDate()}</strong>
          </div>
        </div>
        <div class="event-content">
          <h3>${escapeHtml(event.name)}</h3>
          <p class="description">${escapeHtml(event.description)}</p>
          <div class="ticket-meta">
            <span>${formatDate(event.startsAt)}</span>
            <strong class="${soldOut ? "sold-out" : ""}">${soldOut ? "Hết vé" : `${event.availableTickets} vé còn lại`}</strong>
          </div>
          <div class="progress" title="${soldPercent.toFixed(0)}% đã bán">
            <span style="width:${soldPercent}%"></span>
          </div>
          <button class="primary-button" type="button" data-book="${event.id}" ${soldOut ? "disabled" : ""}>
            ${soldOut ? "Đã hết vé" : "Đặt vé ngay"}
          </button>
        </div>
      </article>`;
  }).join("");
}

function openBooking(eventId) {
  const event = state.events.find(item => item.id === eventId);
  if (!event) return;

  state.selectedEvent = event;
  document.getElementById("booking-event-id").value = event.id;
  document.getElementById("dialog-event-name").textContent = event.name;
  document.getElementById("dialog-event-info").textContent =
    `${formatDate(event.startsAt)} · Còn ${event.availableTickets} vé`;
  document.getElementById("booking-quantity").max = String(Math.min(10, event.availableTickets));
  document.getElementById("booking-error").classList.add("hidden");
  bookingDialog.showModal();
  document.getElementById("booking-user").focus();
}

document.getElementById("event-search").addEventListener("input", event => {
  const query = event.target.value.trim().toLowerCase();
  const filtered = state.events.filter(item =>
    item.name.toLowerCase().includes(query) ||
    item.description.toLowerCase().includes(query));
  renderEvents(filtered);
});

eventGrid.addEventListener("click", event => {
  const button = event.target.closest("[data-book]");
  if (button) openBooking(button.dataset.book);
});

document.querySelector(".dialog-close").addEventListener("click", () => bookingDialog.close());

document.getElementById("booking-form").addEventListener("submit", async event => {
  event.preventDefault();
  const submitButton = event.currentTarget.querySelector("button[type='submit']");
  const errorElement = document.getElementById("booking-error");
  submitButton.disabled = true;

  const payload = {
    eventId: document.getElementById("booking-event-id").value,
    userId: document.getElementById("booking-user").value.trim(),
    quantity: Number(document.getElementById("booking-quantity").value)
  };

  try {
    const booking = await apiFetch("/bookings", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    bookingDialog.close();
    document.getElementById("lookup-user").value = payload.userId;
    state.trackedBookingId = booking.id;
    showToast(`Đã gửi yêu cầu đặt vé. Mã: ${booking.id}`);
    await loadEvents();
    switchView("bookings");
    await lookupBookings(payload.userId);
  } catch (error) {
    errorElement.textContent = error.message;
    errorElement.classList.remove("hidden");
  } finally {
    submitButton.disabled = false;
  }
});

async function lookupBookings(userId) {
  bookingsLoading.classList.remove("hidden");
  bookingList.innerHTML = "";
  try {
    const bookings = await apiFetch(`/users/${encodeURIComponent(userId)}/bookings`);
    renderBookings(bookings);
  } catch (error) {
    bookingList.innerHTML = `<div class="state-card"><p>${escapeHtml(error.message)}</p></div>`;
  } finally {
    bookingsLoading.classList.add("hidden");
  }
}

async function renderBookings(bookings) {
  if (!bookings.length) {
    bookingList.innerHTML = '<div class="state-card"><p>Người dùng này chưa có vé nào.</p></div>';
    return;
  }

  const eventLookup = new Map(state.events.map(event => [event.id, event]));
  const missingEvents = [...new Set(bookings.map(booking => booking.eventId))]
    .filter(id => !eventLookup.has(id));

  if (missingEvents.length) {
    await Promise.all(missingEvents.map(async id => {
      try {
        const event = await apiFetch(`/events/${id}`);
        eventLookup.set(id, event);
      } catch {
        eventLookup.set(id, null);
      }
    }));
  }

  bookingList.innerHTML = bookings.map(booking => {
    const event = eventLookup.get(booking.eventId);
    const status = statusClass(booking.status);
    return `
      <article class="booking-row" data-booking-id="${escapeHtml(booking.id)}">
        <div class="booking-id">${escapeHtml(booking.quantity)} vé</div>
        <div class="booking-main">
          <h3>${escapeHtml(event?.name || "Sự kiện")}</h3>
          <p>Mã: ${escapeHtml(booking.id)} · Tạo lúc ${formatDate(booking.createdAt)}</p>
          ${booking.rejectionReason ? `<p>Lý do: ${escapeHtml(booking.rejectionReason)}</p>` : ""}
        </div>
        <span class="status status-${status}">${statusText(booking.status)}</span>
      </article>`;
  }).join("");

  if (bookings.some(booking => statusClass(booking.status) === "pending")) {
    window.clearTimeout(renderBookings.pollTimer);
    renderBookings.pollTimer = window.setTimeout(() => {
      const userId = document.getElementById("lookup-user").value.trim();
      if (userId) lookupBookings(userId);
    }, 3000);
  }
}

document.getElementById("lookup-form").addEventListener("submit", event => {
  event.preventDefault();
  const userId = document.getElementById("lookup-user").value.trim();
  if (userId) lookupBookings(userId);
});

function switchView(viewName) {
  document.querySelectorAll(".view").forEach(view => view.classList.remove("active"));
  document.querySelectorAll(".nav-link").forEach(link => link.classList.remove("active"));
  document.getElementById(`${viewName}-view`).classList.add("active");
  document.querySelector(`[data-view="${viewName}"]`).classList.add("active");
  window.scrollTo({ top: 0, behavior: "smooth" });
}

document.querySelectorAll(".nav-link").forEach(link => {
  link.addEventListener("click", () => switchView(link.dataset.view));
});

loadEvents();
