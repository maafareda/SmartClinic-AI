const byId = (id) => document.getElementById(id);
const state = { clinics: [], doctors: [], clinic: null, requestId: 0 };
const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (char) => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
})[char]);

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: { "Content-Type": "application/json", ...options.headers }
  });
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) throw new Error(body?.error || `Erreur ${response.status}`);
  return body;
}

function clinicPath() {
  return `/api/clinics/${byId("patient-clinic").value}`;
}

function localDateValue(date) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

function showError(message) {
  const error = byId("patient-error");
  error.textContent = message;
  error.hidden = !message;
}

function renderClinicDetails() {
  const clinic = state.clinics.find((item) => item.id === byId("patient-clinic").value);
  if (!clinic) return;
  state.clinic = clinic;
  byId("patient-clinic-name").textContent = clinic.name;
  byId("patient-clinic-address").textContent = `${clinic.address}, ${clinic.city}`;
  const phone = byId("patient-clinic-phone");
  phone.textContent = clinic.phone;
  phone.href = `tel:${clinic.phone.replace(/[^\d+]/g, "")}`;
}

function updateSubmitState() {
  byId("patient-submit").disabled = !byId("patient-selected-slot").value;
}

async function loadDoctors() {
  const select = byId("patient-doctor");
  select.innerHTML = `<option value="">Chargement des médecins…</option>`;
  byId("patient-selected-slot").value = "";
  updateSubmitState();
  const doctors = await api(`${clinicPath()}/doctors`);
  state.doctors = doctors;
  select.innerHTML = doctors.length
    ? `<option value="">Choisissez un médecin</option>${doctors.map((doctor) =>
      `<option value="${escapeHtml(doctor.id)}">${escapeHtml(doctor.fullName)} · ${escapeHtml(doctor.specialty)}</option>`).join("")}`
    : `<option value="">Aucun médecin disponible</option>`;
  await loadSlots();
}

async function loadSlots() {
  const requestId = ++state.requestId;
  const date = byId("patient-date").value;
  const doctorId = byId("patient-doctor").value;
  const holder = byId("patient-slot-list");
  byId("patient-selected-slot").value = "";
  updateSubmitState();
  showError("");
  if (!date || !doctorId) {
    holder.innerHTML = `<span class="slot-placeholder">Choisissez un médecin et une date.</span>`;
    return;
  }
  holder.innerHTML = `<span class="slot-placeholder">Recherche des créneaux disponibles…</span>`;
  try {
    const slots = await api(`${clinicPath()}/slots?doctorId=${encodeURIComponent(doctorId)}&date=${encodeURIComponent(date)}`);
    if (requestId !== state.requestId) return;
    holder.innerHTML = slots.length ? slots.map((slot) =>
      `<button class="slot-button" type="button" data-slot="${escapeHtml(slot.startAt)}">${new Intl.DateTimeFormat("fr-FR", {
        hour: "2-digit", minute: "2-digit", timeZone: "Africa/Casablanca"
      }).format(new Date(slot.startAt))}</button>`).join("")
      : `<span class="slot-placeholder">Aucun créneau disponible à cette date. Essayez une autre date.</span>`;
  } catch (error) {
    if (requestId !== state.requestId) return;
    holder.innerHTML = `<span class="slot-placeholder">Impossible de charger les créneaux.</span>`;
    showError(error.message);
  }
}

function displayConfirmation(result, formValues) {
  const appointment = result.appointment;
  byId("confirmation-clinic").textContent = state.clinic.name;
  byId("confirmation-doctor").textContent = appointment.doctorName;
  byId("confirmation-date").textContent = new Intl.DateTimeFormat("fr-FR", {
    weekday: "long", day: "numeric", month: "long", year: "numeric",
    hour: "2-digit", minute: "2-digit", timeZone: "Africa/Casablanca"
  }).format(new Date(appointment.startAt));
  byId("confirmation-deposit").textContent = `${new Intl.NumberFormat("fr-MA", {
    maximumFractionDigits: 0
  }).format(appointment.depositAmount)} DH (simulation)`;
  byId("patient-checkin-code").textContent = result.checkInCode;
  byId("patient-booking-form").hidden = true;
  byId("booking-success").hidden = false;
  byId("booking-title").textContent = `Merci ${formValues.firstName.trim()} !`;
}

byId("patient-clinic").addEventListener("change", async () => {
  renderClinicDetails();
  try {
    await loadDoctors();
  } catch (error) {
    showError(error.message);
  }
});

byId("patient-doctor").addEventListener("change", loadSlots);
byId("patient-date").addEventListener("change", loadSlots);

byId("patient-slot-list").addEventListener("click", (event) => {
  const button = event.target.closest("[data-slot]");
  if (!button) return;
  document.querySelectorAll("#patient-slot-list [data-slot]").forEach((slot) =>
    slot.classList.toggle("selected", slot === button));
  byId("patient-selected-slot").value = button.dataset.slot;
  updateSubmitState();
});

byId("patient-booking-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  if (!form.reportValidity() || !byId("patient-selected-slot").value) return;
  const values = Object.fromEntries(new FormData(form));
  const submit = byId("patient-submit");
  submit.disabled = true;
  submit.textContent = "Confirmation en cours…";
  showError("");
  try {
    const result = await api(`${clinicPath()}/appointments`, {
      method: "POST",
      body: JSON.stringify({
        firstName: values.firstName,
        lastName: values.lastName,
        phone: values.phone,
        preferredLanguage: "fr",
        doctorId: values.doctorId,
        startAt: values.startAt
      })
    });
    displayConfirmation(result, values);
    window.scrollTo({ top: 0, behavior: "smooth" });
  } catch (error) {
    showError(error.message);
    await loadSlots();
  } finally {
    submit.textContent = "Confirmer mon rendez-vous →";
    updateSubmitState();
  }
});

byId("patient-new-booking").addEventListener("click", async () => {
  byId("booking-success").hidden = true;
  byId("patient-booking-form").hidden = false;
  byId("booking-title").textContent = "Prendre rendez-vous";
  byId("patient-booking-form").reset();
  byId("patient-selected-slot").value = "";
  byId("patient-date").value = localDateValue(new Date());
  byId("patient-slot-list").innerHTML = `<span class="slot-placeholder">Choisissez un médecin et une date.</span>`;
  updateSubmitState();
  renderClinicDetails();
  try {
    await loadDoctors();
    showError("");
  } catch (error) {
    showError(error.message);
  }
  window.scrollTo({ top: 0, behavior: "smooth" });
});

async function initialize() {
  const date = new Date();
  const maximumDate = new Date(date);
  maximumDate.setDate(maximumDate.getDate() + 30);
  byId("patient-date").min = localDateValue(date);
  byId("patient-date").max = localDateValue(maximumDate);
  byId("patient-date").value = localDateValue(date);
  try {
    state.clinics = await api("/api/clinics");
    if (!state.clinics.length) throw new Error("Aucun cabinet n'est disponible pour la réservation.");
    byId("patient-clinic").innerHTML = state.clinics.map((clinic) =>
      `<option value="${escapeHtml(clinic.id)}">${escapeHtml(clinic.name)} · ${escapeHtml(clinic.city)}</option>`).join("");
    renderClinicDetails();
    await loadDoctors();
  } catch (error) {
    showError(error.message);
    byId("patient-slot-list").innerHTML = `<span class="slot-placeholder">La réservation est temporairement indisponible.</span>`;
  }
}

initialize();
