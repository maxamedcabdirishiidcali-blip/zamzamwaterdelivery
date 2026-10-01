// Zamzam Water Delivery - shared UI behaviour (toasts, confirmations, loading states, AJAX cart).
(function () {
    'use strict';

    const csrfToken = document.querySelector('meta[name="csrf-token"]')?.content || '';
    const loader = document.getElementById('page-loader');

    const setLoading = (on) => loader && loader.classList.toggle('d-none', !on);

    // ---------- Toasts ----------
    function showToast(message, type = 'success') {
        const css = { success: 'text-bg-success', error: 'text-bg-danger', info: 'text-bg-primary' }[type] || 'text-bg-primary';
        const icon = { success: 'bi-check-circle', error: 'bi-exclamation-triangle', info: 'bi-info-circle' }[type] || 'bi-info-circle';
        const el = document.createElement('div');
        el.className = `toast align-items-center border-0 ${css}`;
        el.setAttribute('role', 'alert');
        el.innerHTML = `<div class="d-flex"><div class="toast-body"><i class="bi ${icon} me-2"></i></div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>`;
        el.querySelector('.toast-body').append(document.createTextNode(message));
        document.getElementById('toast-container').appendChild(el);
        new bootstrap.Toast(el, { delay: 5000 }).show();
        el.addEventListener('hidden.bs.toast', () => el.remove());
    }
    window.showToast = showToast;
    document.querySelectorAll('#toast-container .toast').forEach(t => new bootstrap.Toast(t).show());

    // ---------- AJAX helper ----------
    async function postForm(url, data) {
        setLoading(true);
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: { 'RequestVerificationToken': csrfToken, 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
                body: data
            });
            if (response.status === 401) { window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(location.pathname); return null; }
            const json = await response.json().catch(() => ({ success: false, message: 'Unexpected server response.' }));
            if (!response.ok || json.success === false) { showToast(json.message || 'Something went wrong.', 'error'); return null; }
            return json;
        } catch {
            showToast('Network error. Please check your connection.', 'error');
            return null;
        } finally {
            setLoading(false);
        }
    }

    const money = (v) => (window.zwCurrency || '$') + Number(v).toFixed(2);

    function updateCartBadge(count) {
        const badge = document.getElementById('cart-count');
        if (!badge) return;
        badge.textContent = count;
        badge.classList.toggle('d-none', count === 0);
    }

    function updateCartSummary(summary) {
        updateCartBadge(summary.itemCount);
        const set = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = money(value); };
        set('cart-subtotal', summary.subTotal);
        set('cart-delivery', summary.deliveryFee);
        set('cart-total', summary.total);
    }

    // ---------- Add to cart (AJAX) ----------
    document.addEventListener('submit', async (e) => {
        const form = e.target;
        if (!form.classList.contains('add-to-cart-form')) return;
        e.preventDefault();
        const result = await postForm(form.action, new FormData(form));
        if (result) {
            updateCartBadge(result.summary.itemCount);
            showToast(result.message || 'Added to your cart.');
        }
    });

    // ---------- Cart quantity stepper ----------
    document.querySelectorAll('.cart-row').forEach(row => {
        const input = row.querySelector('.qty-value');
        let timer;
        const commit = async () => {
            const qty = parseInt(input.value, 10);
            if (!qty || qty < 1) { input.value = 1; return; }
            const data = new FormData();
            data.append('CartItemId', row.dataset.itemId);
            data.append('Quantity', qty);
            const result = await postForm('/Cart/Update', data);
            if (result) {
                updateCartSummary(result.summary);
                if (result.summary.lineTotal != null) row.querySelector('.line-total').textContent = money(result.summary.lineTotal);
            } else {
                location.reload();
            }
        };
        row.querySelectorAll('[data-step]').forEach(btn => btn.addEventListener('click', () => {
            const max = parseInt(input.max, 10) || 500;
            input.value = Math.min(max, Math.max(1, (parseInt(input.value, 10) || 1) + parseInt(btn.dataset.step, 10)));
            clearTimeout(timer);
            timer = setTimeout(commit, 350);
        }));
        input.addEventListener('change', commit);
    });

    // ---------- Confirmation dialogs ----------
    const modalEl = document.getElementById('confirmModal');
    let pendingForm = null;
    document.addEventListener('submit', (e) => {
        const form = e.target;
        if (!form.dataset.confirm || form.dataset.confirmed === 'true') return;
        e.preventDefault();
        pendingForm = form;
        document.getElementById('confirmModalBody').textContent = form.dataset.confirm;
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    }, true);
    document.getElementById('confirmModalOk')?.addEventListener('click', () => {
        if (!pendingForm) return;
        pendingForm.dataset.confirmed = 'true';
        bootstrap.Modal.getInstance(modalEl).hide();
        setLoading(true);
        pendingForm.requestSubmit ? pendingForm.requestSubmit() : pendingForm.submit();
    });

    // ---------- Loading state on submit buttons ----------
    document.addEventListener('submit', (e) => {
        const form = e.target;
        if (e.defaultPrevented || form.classList.contains('add-to-cart-form')) return;
        if (window.jQuery && jQuery(form).data('validator') && !jQuery(form).valid()) return;
        const btn = form.querySelector('button[data-loading]');
        if (btn) {
            btn.disabled = true;
            btn.innerHTML = `<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>${btn.dataset.loading}`;
        }
    });

    // ---------- Clickable table rows ----------
    document.querySelectorAll('tr.clickable-row').forEach(row => row.addEventListener('click', (e) => {
        if (e.target.closest('a, button, form, input, select')) return;
        window.location.href = row.dataset.href;
    }));

    // ---------- Image preview ----------
    document.querySelectorAll('input[type=file][data-preview]').forEach(input => input.addEventListener('change', () => {
        const file = input.files?.[0];
        const img = document.querySelector(input.dataset.preview);
        if (file && img) img.src = URL.createObjectURL(file);
    }));
})();
