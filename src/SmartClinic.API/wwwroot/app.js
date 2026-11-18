const state = {
  clinics: [],
  clinicId: null,
  clinic: null,
  doctors: [],
  appointments: [],
  queue: [],
  requests: [],
  page: "dashboard",
  language: "fr"
};

const byId = (id) => document.getElementById(id);
const clinicPath = () => `/api/clinics/${state.clinicId}`;
const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (char) => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
})[char]);

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: { "Content-Type": "application/json", ...options.headers }
  });
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(body?.error || `Erreur ${response.status}`);
  }
  return body;
}

function toast(message, isError = false) {
  const item = document.createElement("div");
  item.className = `toast${isError ? " error" : ""}`;
  item.textContent = message;
  byId("toast-region").append(item);
  window.setTimeout(() => item.remove(), 4200);
}

function statusLabel(status) {
  return ({
    PendingPayment: "En attente de paiement",
    Confirmed: "Confirmé",
    CheckedIn: "Arrivé",
    Waiting: "En attente",
    InProgress: "En consultation",
    Completed: "Terminé",
    Settled: "Réglé",
    Cancelled: "Annulé",
    NoShow: "Absent",
    Expired: "Expiré",
    Pending: "À traiter",
    Resolved: "Résolue",
    Rejected: "Refusée",
    InProgressRequest: "En cours",
    WaitingForPatient: "En attente patient"
  })[status] || status;
}

function statusClass(status) {
  return String(status || "").toLowerCase().replace(/[^a-z]/g, "");
}

function time(value) {
  return new Intl.DateTimeFormat(state.language === "ar" ? "ar-MA" : "fr-FR", {
    hour: "2-digit", minute: "2-digit", timeZone: "Africa/Casablanca"
  }).format(new Date(value));
}

function dateTime(value) {
  return new Intl.DateTimeFormat(state.language === "ar" ? "ar-MA" : "fr-FR", {
    day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit",
    timeZone: "Africa/Casablanca"
  }).format(new Date(value));
}

function money(amount) {
  return new Intl.NumberFormat(state.language === "ar" ? "ar-MA" : "fr-MA", {
    maximumFractionDigits: 0
  }).format(amount) + " DH";
}

function patientAvatar(name, index = 0) {
  const initials = String(name || "?").split(/\s+/).slice(0, 2).map((part) => part[0] || "").join("").toUpperCase();
  return `<span class="patient-avatar avatar-${index % 4}">${escapeHtml(initials)}</span>`;
}

function patientCell(appointment, index) {
  return `<div class="patient-cell">${patientAvatar(appointment.patientName, index)}<span>${escapeHtml(appointment.patientName)}</span></div>`;
}

function statusBadge(status) {
  return `<span class="status ${statusClass(status)}">${escapeHtml(statusLabel(status))}</span>`;
}

function actionButton(appointment, compact = false) {
  const id = escapeHtml(appointment.id);
  if (appointment.status === "Waiting") {
    return `<button class="row-action" data-action="start" data-id="${id}">Démarrer</button>`;
  }
  if (appointment.status === "InProgress") {
    return `<button class="row-action" data-action="complete" data-id="${id}">Terminer</button>`;
  }
  if (appointment.status === "Completed") {
    return `<button class="row-action" data-action="balance" data-id="${id}">Encaisser</button>`;
  }
  if (appointment.status === "Confirmed") {
    return `<button class="row-action" data-action="manual-check-in" data-id="${id}">${compact ? "Arrivée manuelle" : "Check-in"}</button>`;
  }
  return `<button class="row-action" disabled>${escapeHtml(statusLabel(appointment.status))}</button>`;
}

function renderQueueRows(items, full = false) {
  if (!items.length) {
    return `<tr><td class="empty-state" colspan="${full ? 7 : 5}">Aucun patient dans la file pour le moment.</td></tr>`;
  }
  return items.map((appointment, index) => full
    ? `<tr><td><span class="queue-number">${String(index + 1).padStart(2, "0")}</span></td><td>${patientCell(appointment, index)}</td><td>${escapeHtml(appointment.doctorName)}</td><td class="time-cell">${time(appointment.startAt)}</td><td>${appointment.checkedInAt ? time(appointment.checkedInAt) : "—"}</td><td>${statusBadge(appointment.status)}</td><td>${actionButton(appointment)}</td></tr>`
    : `<tr><td>${patientCell(appointment, index)}</td><td>${escapeHtml(appointment.doctorName)}</td><td class="time-cell">${time(appointment.startAt)}</td><td>${statusBadge(appointment.status)}</td><td>${actionButton(appointment, true)}</td></tr>`
  ).join("");
}

function requestItem(request, compact = false) {
  const urgent = request.priority === "Urgent" || request.priority === "High";
  const icon = request.type === "Reschedule" ? "◷" : request.type === "Cancel" ? "×" : urgent ? "!" : "✳";
  return `<article class="request-item"><span class="request-type-icon${urgent ? " urgent" : ""}">${icon}</span><div class="request-info"><strong>${escapeHtml(request.summary)}</strong><p>${escapeHtml(request.description)}</p><span class="priority${urgent ? " urgent" : ""}">${escapeHtml(request.priority === "Urgent" ? "Urgente" : request.priority === "High" ? "Prioritaire" : request.channel)}</span></div>${compact ? `<time class="request-time">${dateTime(request.createdAt)}</time>` : ""}</article>`;
}

function renderRequests() {
  const pending = state.requests.filter((item) => item.status !== "Resolved" && item.status !== "Rejected");
  byId("requests-nav-count").textContent = String(pending.length);
  byId("stat-requests").textContent = String(pending.length);
  byId("recent-requests").innerHTML = pending.length
    ? pending.slice(0, 3).map((item) => requestItem(item, true)).join("")
    : `<div class="empty-state">Aucune demande en attente.</div>`;
  byId("request-inbox").innerHTML = state.requests.length ? state.requests.map((request) => `
    <article class="inbox-item">
      <div class="inbox-main"><span class="request-type-icon${request.priority === "Urgent" ? " urgent" : ""}">${request.priority === "Urgent" ? "!" : "✳"}</span><div class="inbox-copy">
        <strong>${escapeHtml(request.summary)}</strong><p>${escapeHtml(request.description)}</p>
        <div class="inbox-meta"><span>${escapeHtml(request.channel)}</span><span>·</span><time>${dateTime(request.createdAt)}</time><span>·</span>${statusBadge(request.status)}</div>
      </div></div>
      <div class="inbox-actions"><select aria-label="Statut de la demande" data-request-status="${escapeHtml(request.id)}">
        ${["Pending", "InProgress", "WaitingForPatient", "Resolved", "Rejected"].map((status) =>
    `<option value="${status}"${request.status === status ? " selected" : ""}>${escapeHtml(statusLabel(status))}</option>`).join("")}
      </select></div>
    </article>`).join("") : `<div class="empty-state">Aucune demande patient.</div>`;
}

function renderAppointments() {
  const sorted = [...state.appointments].sort((a, b) => new Date(a.startAt) - new Date(b.startAt));
  byId("appointments-nav-count").textContent = String(sorted.length);
  byId("appointments-total").textContent = `${sorted.length} rendez-vous`;
  byId("appointments-table").innerHTML = sorted.length ? sorted.map((appointment, index) => `
    <tr><td>${patientCell(appointment, index)}</td><td>${escapeHtml(appointment.doctorName)}<small class="table-subline">${escapeHtml(appointment.specialty)}</small></td>
    <td class="time-cell">${dateTime(appointment.startAt)}</td><td>${money(appointment.depositAmount)}</td><td>${money(appointment.remainingAmount)}</td>
    <td>${statusBadge(appointment.status)}</td><td>${actionButton(appointment)}</td></tr>`).join("")
    : `<tr><td colspan="7" class="empty-state">Aucun rendez-vous dans ce cabinet.</td></tr>`;
  byId("queue-table").innerHTML = renderQueueRows(state.queue.slice(0, 4));
  byId("full-queue-table").innerHTML = renderQueueRows(state.queue, true);
  renderSchedule(sorted);
}

function renderSchedule(appointments) {
  const display = appointments.filter((item) => ["Waiting", "InProgress", "Confirmed", "CheckedIn"].includes(item.status)).slice(0, 4);
  byId("schedule-list").innerHTML = display.length ? display.map((appointment, index) => `
    <div class="schedule-item"><time class="schedule-time">${time(appointment.startAt)}</time><span class="timeline-marker${index % 2 ? " blue-marker" : ""}"></span>
      <div class="schedule-person"><strong>${escapeHtml(appointment.patientName)}</strong><small>${escapeHtml(appointment.doctorName)} · ${escapeHtml(appointment.specialty)}</small></div>
      <span class="schedule-tag">${escapeHtml(statusLabel(appointment.status))}</span></div>`).join("")
    : `<div class="empty-state">Aucun rendez-vous actif pour aujourd'hui.</div>`;
}

function renderDoctors() {
  byId("doctor-grid").innerHTML = state.doctors.length ? state.doctors.map((doctor) => `
    <article class="doctor-card"><span class="doctor-avatar">${escapeHtml(doctor.fullName.split(" ").slice(-1)[0].slice(0, 2).toUpperCase())}</span>
      <h2>${escapeHtml(doctor.fullName)}</h2><p>${escapeHtml(doctor.specialty)}</p>
      <div class="doctor-meta"><span>Consultation</span><strong>${money(doctor.consultationPrice)}</strong></div></article>`).join("")
    : `<div class="empty-state">Aucun médecin déclaré.</div>`;
}

function renderSettings() {
  const clinic = state.clinic;
  const doctor = state.doctors[0];
  const rows = [
    ["Cabinet", clinic.name], ["Adresse", `${clinic.address}, ${clinic.city}`],
    ["Téléphone", clinic.phone], ["Fuseau horaire", clinic.timezone],
    ["Avance", money(clinic.settings.depositAmount)], ["Durée d'un créneau", `${clinic.settings.slotDurationMinutes} minutes`],
    ["Tolérance de retard", `${clinic.settings.lateToleranceMinutes} minutes`],
    ["Prix de consultation", doctor ? money(doctor.consultationPrice) : "—"]
  ];
  byId("clinic-settings").innerHTML = rows.map(([label, value]) => `<div class="setting-row"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`).join("");
}

async function refresh() {
  if (!state.clinicId) return;
  const [clinic, doctors, dashboard, appointments, queue, requests] = await Promise.all([
    api("/api/clinics").then((clinics) => clinics.find((item) => item.id === state.clinicId)),
    api(`${clinicPath()}/doctors`),
    api(`${clinicPath()}/dashboard`),
    api(`${clinicPath()}/appointments`),
    api(`${clinicPath()}/queue/today`),
    api(`${clinicPath()}/requests`)
  ]);
  if (!clinic) throw new Error("Cabinet introuvable.");
  state.clinic = clinic;
  state.doctors = doctors;
  state.appointments = appointments;
  state.queue = queue;
  state.requests = requests;
  byId("sidebar-clinic").textContent = clinic.name;
  byId("stat-appointments").textContent = String(dashboard.appointmentsToday);
  byId("stat-waiting").textContent = String(dashboard.waitingPatients);
  byId("stat-revenue").textContent = money(dashboard.revenueToday);
  byId("request-trend").textContent = dashboard.pendingRequests ? "À traiter" : "À jour";
  byId("schedule-date").textContent = new Intl.DateTimeFormat("fr-FR", {
    weekday: "long", day: "numeric", month: "long", timeZone: "Africa/Casablanca"
  }).format(new Date());
  byId("appointments-table").dataset.loaded = "true";
  renderAppointments();
  renderRequests();
  renderDoctors();
  renderSettings();
  updateBookingDoctors();
}

function updateBookingDoctors() {
  const select = byId("doctor-select");
  const selected = select.value;
  select.innerHTML = state.doctors.map((doctor) =>
    `<option value="${escapeHtml(doctor.id)}">${escapeHtml(doctor.fullName)} · ${escapeHtml(doctor.specialty)}</option>`).join("");
  if (state.doctors.some((doctor) => doctor.id === selected)) select.value = selected;
}

function navigate(page) {
  state.page = page;
  document.querySelectorAll(".page-section").forEach((section) => section.classList.toggle("active", section.id === `page-${page}`));
  document.querySelectorAll(".nav-item[data-page]").forEach((item) => item.classList.toggle("active", item.dataset.page === page));
  const active = document.querySelector(`#page-${page} h1`);
  byId("breadcrumb-current").textContent = active?.textContent || "Vue d'ensemble";
  byId("sidebar").classList.remove("open");
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function localDateValue(date) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

async function loadSlots() {
  const date = byId("appointment-date").value;
  const doctorId = byId("doctor-select").value;
  const holder = byId("slot-list");
  byId("selected-slot").value = "";
  byId("booking-submit").disabled = true;
  if (!date || !doctorId) {
    holder.innerHTML = `<span class="slot-placeholder">Choisissez une date pour afficher les disponibilités.</span>`;
    return;
  }
  holder.innerHTML = `<span class="slot-placeholder">Recherche des créneaux…</span>`;
  try {
    const slots = await api(`${clinicPath()}/slots?doctorId=${encodeURIComponent(doctorId)}&date=${encodeURIComponent(date)}`);
    holder.innerHTML = slots.length ? slots.map((slot) =>
      `<button class="slot-button" type="button" data-slot="${escapeHtml(slot.startAt)}">${time(slot.startAt)}</button>`).join("")
      : `<span class="slot-placeholder">Aucun créneau disponible à cette date.</span>`;
  } catch (error) {
    holder.innerHTML = `<span class="slot-placeholder">${escapeHtml(error.message)}</span>`;
  }
}

function openBooking() {
  const dialog = byId("booking-dialog");
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  byId("appointment-date").min = localDateValue(new Date());
  byId("appointment-date").value = localDateValue(tomorrow);
  byId("selected-slot").value = "";
  byId("booking-submit").disabled = true;
  byId("slot-list").innerHTML = `<span class="slot-placeholder">Choisissez un créneau disponible.</span>`;
  dialog.showModal();
  loadSlots();
}

function appendChatMessage(message, outgoing) {
  const row = document.createElement("div");
  row.className = `message-row${outgoing ? " outgoing" : ""}`;
  const bubble = document.createElement("div");
  bubble.className = "message-bubble";
  const paragraph = document.createElement("p");
  paragraph.textContent = message;
  const timestamp = document.createElement("time");
  timestamp.textContent = new Intl.DateTimeFormat("fr-FR", { hour: "2-digit", minute: "2-digit" }).format(new Date());
  bubble.append(paragraph, timestamp);
  if (!outgoing) {
    const avatar = document.createElement("span");
    avatar.className = "chat-avatar";
    avatar.textContent = "✦";
    row.append(avatar);
  }
  row.append(bubble);
  byId("chat-messages").append(row);
  byId("chat-messages").scrollTop = byId("chat-messages").scrollHeight;
}

async function updateAppointmentAction(button) {
  const { action, id } = button.dataset;
  const paths = {
    start: "start", complete: "complete", balance: "balance", "manual-check-in": "manual-check-in"
  };
  const payload = {};
  if (action === "manual-check-in") {
    const reason = window.prompt("Motif du check-in manuel (à des fins de traçabilité) :");
    if (!reason?.trim()) return;
    payload.reason = reason.trim();
  }
  await api(`${clinicPath()}/appointments/${id}/${paths[action]}`, {
    method: "POST",
    body: JSON.stringify(payload)
  });
  toast(action === "balance" ? "Solde encaissé (simulation)." : "Rendez-vous mis à jour.");
  await refresh();
}

document.querySelectorAll("[data-page]").forEach((button) => {
  button.addEventListener("click", () => navigate(button.dataset.page));
});

document.querySelectorAll("[data-close-dialog]").forEach((button) => {
  button.addEventListener("click", () => byId("booking-dialog").close());
});

byId("new-appointment").addEventListener("click", openBooking);
byId("new-appointment-secondary").addEventListener("click", openBooking);
byId("mobile-menu").addEventListener("click", () => byId("sidebar").classList.toggle("open"));
byId("appointment-date").addEventListener("change", loadSlots);
byId("doctor-select").addEventListener("change", loadSlots);

document.querySelector(".clinic-switcher").addEventListener("change", async (event) => {
  state.clinicId = event.target.value;
  try {
    await refresh();
    toast(`Cabinet sélectionné : ${state.clinic.name}`);
  } catch (error) {
    toast(error.message, true);
  }
});

byId("slot-list").addEventListener("click", (event) => {
  const button = event.target.closest("[data-slot]");
  if (!button) return;
  document.querySelectorAll(".slot-button").forEach((slot) => slot.classList.toggle("selected", slot === button));
  byId("selected-slot").value = button.dataset.slot;
  byId("booking-submit").disabled = false;
});

byId("booking-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  const command = {
    firstName: form.get("firstName"),
    lastName: form.get("lastName"),
    phone: form.get("phone"),
    preferredLanguage: state.language,
    doctorId: form.get("doctorId"),
    startAt: form.get("startAt")
  };
  try {
    const result = await api(`${clinicPath()}/appointments`, { method: "POST", body: JSON.stringify(command) });
    byId("booking-dialog").close();
    byId("booking-form").reset();
    byId("booking-code").textContent = result.checkInCode;
    byId("code-dialog").showModal();
    toast(`Rendez-vous confirmé · avance simulée de ${money(result.appointment.depositAmount)}.`);
    await refresh();
  } catch (error) {
    toast(error.message, true);
    await loadSlots();
  }
});

document.addEventListener("click", async (event) => {
  const button = event.target.closest("[data-action]");
  if (!button || button.disabled) return;
  try {
    await updateAppointmentAction(button);
  } catch (error) {
    toast(error.message, true);
  }
});

byId("request-inbox").addEventListener("change", async (event) => {
  const select = event.target.closest("[data-request-status]");
  if (!select) return;
  let resolutionNote = null;
  if (select.value === "Resolved") {
    resolutionNote = window.prompt("Note de résolution (obligatoire) :");
    if (!resolutionNote?.trim()) {
      await refresh();
      return;
    }
  }
  try {
    await api(`${clinicPath()}/requests/${select.dataset.requestStatus}`, {
      method: "PATCH",
      body: JSON.stringify({ status: select.value, resolutionNote })
    });
    toast("Statut de la demande mis à jour.");
    await refresh();
  } catch (error) {
    toast(error.message, true);
    await refresh();
  }
});

byId("chat-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const input = byId("chat-input");
  const message = input.value.trim();
  if (!message) return;
  appendChatMessage(message, true);
  input.value = "";
  try {
    const result = await api(`${clinicPath()}/ai/chat`, {
      method: "POST",
      body: JSON.stringify({ message, language: byId("chat-language").value })
    });
    appendChatMessage(result.reply, false);
    if (result.requestId) {
      toast(result.intent === "Urgent" ? "Demande urgente transmise à l'accueil." : "Demande transmise à l'accueil.");
      await refresh();
    }
  } catch (error) {
    appendChatMessage(`Je ne peux pas traiter cette demande pour le moment : ${error.message}`, false);
  }
});

byId("language-toggle").addEventListener("click", () => {
  state.language = state.language === "fr" ? "ar" : "fr";
  document.documentElement.lang = state.language;
  document.documentElement.dir = state.language === "ar" ? "rtl" : "ltr";
  byId("language-toggle").textContent = state.language === "ar" ? "FR" : "ع";
  byId("chat-language").value = state.language;
  renderAppointments();
  renderRequests();
  renderDoctors();
  renderSettings();
});

async function initialize() {
  try {
    state.clinics = await api("/api/clinics");
    if (!state.clinics.length) throw new Error("Aucun cabinet de démonstration disponible.");
    state.clinicId = state.clinics[0].id;
    const selector = document.createElement("select");
    selector.id = "clinic-switcher";
    selector.className = "clinic-select";
    selector.setAttribute("aria-label", "Changer de cabinet");
    selector.innerHTML = state.clinics.map((clinic) =>
      `<option value="${escapeHtml(clinic.id)}">${escapeHtml(clinic.name)}</option>`).join("");
    document.querySelector(".clinic-switcher").append(selector);
    byId("eyebrow-date").textContent = new Intl.DateTimeFormat("fr-FR", {
      weekday: "long", day: "numeric", month: "long"
    }).format(new Date()).toLocaleUpperCase("fr-FR");
    await refresh();
    window.setInterval(() => refresh().catch((error) => console.error("Échec du rafraîchissement du tableau de bord", error)), 15000);
  } catch (error) {
    toast(error.message, true);
    console.error("Impossible de charger SmartClinic AI", error);
  }
}

initialize();
