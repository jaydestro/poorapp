// ============================================================
// ShopGlobal Frontend — "First-time Cosmos DB user" edition
//
// Looks professional. Performs terribly. The bottleneck is the
// database design, partition strategy, and query patterns.
// ============================================================

const API = '';
const DEMO_CUSTOMER_ID = 'demo-customer-001';
function slugify(name) { return (name || '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, ''); }
let cartPollInterval = null;
let requestCount = 0;
let totalLatency = 0;

// ============================================================
// Performance tracker — shows request count and avg latency
// ============================================================
function initPerfOverlay() {
    const div = document.createElement('div');
    div.className = 'perf-overlay';
    div.id = 'perfOverlay';
    div.innerHTML = `
        <h4><i class="fas fa-tachometer-alt"></i> Performance</h4>
        <div class="perf-row"><span class="label">Requests</span><span class="val" id="perfReqs">0</span></div>
        <div class="perf-row"><span class="label">Avg Latency</span><span class="val" id="perfLatency">0ms</span></div>
        <div class="perf-row"><span class="label">Last Call</span><span class="val" id="perfLast">—</span></div>
        <div class="perf-row"><span class="label">Errors</span><span class="val" id="perfErrors">0</span></div>
    `;
    document.body.appendChild(div);
}

let errorCount = 0;

async function apiFetch(url, options = {}) {
    const start = performance.now();
    requestCount++;
    updatePerfOverlay();

    try {
        const resp = await fetch(API + url, {
            headers: { 'Content-Type': 'application/json' },
            ...options
        });
        const elapsed = performance.now() - start;
        totalLatency += elapsed;
        updatePerfOverlay(elapsed);

        if (!resp.ok) {
            errorCount++;
            updatePerfOverlay(elapsed);
            console.error(`API ${resp.status}: ${url}`);
            return null;
        }
        return await resp.json();
    } catch (e) {
        const elapsed = performance.now() - start;
        totalLatency += elapsed;
        errorCount++;
        updatePerfOverlay(elapsed);
        console.error(`API error: ${url}`, e);
        return null;
    }
}

function updatePerfOverlay(lastMs) {
    const reqs = document.getElementById('perfReqs');
    const lat = document.getElementById('perfLatency');
    const last = document.getElementById('perfLast');
    const errs = document.getElementById('perfErrors');
    if (!reqs) return;

    reqs.textContent = requestCount;
    const avg = requestCount > 0 ? Math.round(totalLatency / requestCount) : 0;
    lat.textContent = avg + 'ms';
    lat.className = 'val ' + (avg > 500 ? 'slow' : 'ok');

    if (lastMs !== undefined) {
        const ms = Math.round(lastMs);
        last.textContent = ms + 'ms';
        last.className = 'val ' + (ms > 500 ? 'slow' : 'ok');
    }
    errs.textContent = errorCount;
    errs.className = 'val ' + (errorCount > 0 ? 'slow' : 'ok');
}

// ============================================================
// Toast notifications
// ============================================================
function showToast(msg) {
    let toast = document.getElementById('toast');
    if (!toast) {
        toast = document.createElement('div');
        toast.id = 'toast';
        toast.className = 'toast';
        document.body.appendChild(toast);
    }
    toast.textContent = msg;
    toast.classList.add('show');
    setTimeout(() => toast.classList.remove('show'), 3000);
}

// ============================================================
// Loading overlay
// ============================================================
function showLoading(text) {
    document.getElementById('loadingText').textContent = text || 'Loading...';
    document.getElementById('loadingOverlay').style.display = 'flex';
}
function hideLoading() {
    document.getElementById('loadingOverlay').style.display = 'none';
}

// ============================================================
// Router
// ============================================================
function navigateTo(page, data) {
    stopCartPolling();
    const main = document.getElementById('mainContent');
    window.scrollTo({ top: 0, behavior: 'smooth' });

    switch (page) {
        case 'home': renderHome(); break;
        case 'category': renderCategory(data); break;
        case 'product': renderProduct(data); break;
        case 'cart': renderCart(); break;
        case 'account': renderAccount(); break;
        case 'admin': renderAdmin(); break;
        default: renderHome();
    }
}

// ============================================================
// HOME — loads ALL products from EVERY category separately
// (5 separate API calls instead of one)
// ============================================================
async function renderHome() {
    const main = document.getElementById('mainContent');
    showLoading('Loading storefront...');

    main.innerHTML = `
        <div class="hero">
            <h1>Shop the World</h1>
            <p>Discover amazing products from across the globe, delivered to your door in days.</p>
            <button class="hero-btn" onclick="browseCategory('Electronics')">
                <i class="fas fa-bolt"></i> Shop Now
            </button>
        </div>
        <div id="homeCategories"></div>
    `;

    const container = document.getElementById('homeCategories');
    const categories = ['Electronics', 'Clothing', 'Home', 'Books', 'Food'];

    // Intentionally fetching each category one at a time, sequentially
    for (const cat of categories) {
        const products = await apiFetch(`/api/products/category/${cat}`);
        if (!products) continue;

        // Then for EACH product, also fetch its inventory individually
        // This is wildly unnecessary but "the developer wanted stock counts on the homepage"
        const productsWithStock = [];
        for (const p of products.slice(0, 8)) {
            const inv = await apiFetch(`/api/inventory/${p.id}`);
            productsWithStock.push({ ...p, liveInventory: inv });
        }

        container.innerHTML += `
            <div class="section-header">
                <h2>${cat}</h2>
                <a onclick="browseCategory('${cat}')">View All <i class="fas fa-arrow-right"></i></a>
            </div>
            <div class="product-grid">
                ${productsWithStock.map(p => productCard(p)).join('')}
            </div>
        `;
    }

    // Also start polling the cart in the background
    startCartPolling();
    hideLoading();
}

function productCard(p) {
    const avgRating = p.reviews && p.reviews.length > 0
        ? (p.reviews.reduce((s, r) => s + r.rating, 0) / p.reviews.length).toFixed(1)
        : 'N/A';
    const stars = p.reviews ? '★'.repeat(Math.round(avgRating)) + '☆'.repeat(5 - Math.round(avgRating)) : '';
    const totalStock = p.liveInventory ? p.liveInventory.totalStock : '—';

    return `
        <div class="product-card" onclick="navigateTo('product', '${p.id}')">
            <div class="product-card-img"><img src="/images/${slugify(p.name)}.svg" alt="${escHtml(p.name)}" style="width:100%;height:100%;object-fit:contain" onerror="this.outerHTML='<i class=\'fas fa-cube\'></i>'"/></div>
            <div class="product-card-body">
                <div class="category-tag">${p.category || ''}</div>
                <h3>${escHtml(p.name)}</h3>
                <div class="price">$${(p.price || 0).toFixed(2)}</div>
                <div class="rating">${stars} <span style="color:#999">(${p.reviews ? p.reviews.length : 0})</span></div>
                <div class="stock-info"><i class="fas fa-box"></i> ${totalStock} in stock</div>
            </div>
        </div>
    `;
}

// ============================================================
// CATEGORY — loads all products, no pagination
// ============================================================
async function browseCategory(cat) {
    showLoading(`Loading ${cat}...`);
    const products = await apiFetch(`/api/products/category/${cat}`);

    if (!products) {
        hideLoading();
        document.getElementById('mainContent').innerHTML = `
            <h2>${cat}</h2><p>Failed to load products. The database may be overloaded.</p>`;
        return;
    }

    // Intentionally fetch inventory for EVERY product in the category
    const withStock = [];
    for (const p of products) {
        const inv = await apiFetch(`/api/inventory/${p.id}`);
        withStock.push({ ...p, liveInventory: inv });
    }

    const main = document.getElementById('mainContent');
    main.innerHTML = `
        <div class="section-header">
            <h2>${cat} <span style="color:#999; font-weight:300">(${withStock.length} products)</span></h2>
            <a onclick="navigateTo('home')"><i class="fas fa-arrow-left"></i> Back</a>
        </div>
        <div class="product-grid">
            ${withStock.map(p => productCard(p)).join('')}
        </div>
    `;
    hideLoading();
}

// ============================================================
// SEARCH — fires on every keystroke, no debounce
// ============================================================
async function onSearchInput(val) {
    const dropdown = document.getElementById('searchResults');
    if (val.length < 1) {
        dropdown.classList.remove('show');
        return;
    }

    // Fire immediately on every character — no debounce
    const results = await apiFetch(`/api/products/search?q=${encodeURIComponent(val)}`);
    if (!results || results.length === 0) {
        dropdown.innerHTML = '<div class="search-item"><span>No results</span></div>';
        dropdown.classList.add('show');
        return;
    }

    // For each search result, ALSO fetch its inventory (completely unnecessary for search)
    let html = '';
    for (const p of results.slice(0, 10)) {
        const inv = await apiFetch(`/api/inventory/${p.id}`);
        html += `
            <div class="search-item" onclick="navigateTo('product', '${p.id}'); document.getElementById('searchResults').classList.remove('show');">
                <div style="width:40px;height:40px;background:#f0f0f0;border-radius:6px;display:flex;align-items:center;justify-content:center;color:#ccc;overflow:hidden"><img src="/images/${slugify(p.name)}.svg" alt="" style="width:100%;height:100%;object-fit:contain" onerror="this.outerHTML='<i class=\'fas fa-cube\'></i>'"/></div>
                <div>
                    <div style="font-weight:500">${escHtml(p.name)}</div>
                    <div style="font-size:12px;color:#888">$${(p.price || 0).toFixed(2)} · ${inv ? inv.totalStock : '?'} in stock</div>
                </div>
            </div>
        `;
    }
    dropdown.innerHTML = html;
    dropdown.classList.add('show');
}

// Close dropdown when clicking outside
document.addEventListener('click', (e) => {
    if (!e.target.closest('.search-bar')) {
        document.getElementById('searchResults')?.classList.remove('show');
    }
});

// ============================================================
// PRODUCT DETAIL — triggers recommendation rebuild on the backend
// Also loads inventory, all reviews, and recommendations
// ============================================================
async function renderProduct(productId) {
    showLoading('Loading product details...');

    // This GET triggers RebuildRecommendationsAsync() on the server
    const product = await apiFetch(`/api/products/${productId}`);
    if (!product) {
        hideLoading();
        document.getElementById('mainContent').innerHTML = '<p>Product not found.</p>';
        return;
    }

    // Fetch inventory separately (even though it's also embedded in the product)
    const inventory = await apiFetch(`/api/inventory/${productId}`);

    // Fetch recommendations (which were just rebuilt by the product GET above)
    const recs = await apiFetch(`/api/products/${productId}/recommendations`);

    // If we have recommendation product IDs, fetch EACH recommended product's full details
    // This triggers RebuildRecommendationsAsync for each one on the server!
    const recProducts = [];
    if (recs) {
        for (const r of recs.slice(0, 6)) {
            const rp = await apiFetch(`/api/products/${r.productId}`);
            if (rp) {
                const ri = await apiFetch(`/api/inventory/${r.productId}`);
                recProducts.push({ ...rp, liveInventory: ri, score: r.score });
            }
        }
    }

    const avgRating = product.reviews && product.reviews.length > 0
        ? (product.reviews.reduce((s, r) => s + r.rating, 0) / product.reviews.length).toFixed(1)
        : 'N/A';

    const main = document.getElementById('mainContent');
    main.innerHTML = `
        <a onclick="navigateTo('home')" style="color:#e94560;cursor:pointer;font-size:14px">
            <i class="fas fa-arrow-left"></i> Back to Shop
        </a>

        <div class="product-detail" style="margin-top:16px">
            <div class="product-detail-img"><img src="/images/${slugify(product.name)}.svg" alt="${escHtml(product.name)}" style="width:100%;height:100%;object-fit:contain" onerror="this.outerHTML='<i class=\'fas fa-cube\'></i>'"/></div>
            <div class="product-detail-info">
                <div class="category-tag">${product.category}</div>
                <h1>${escHtml(product.name)}</h1>
                <div style="color:#f0a500;margin-bottom:8px">${'★'.repeat(Math.round(avgRating))}${'☆'.repeat(5 - Math.round(avgRating))} ${avgRating} (${product.reviews ? product.reviews.length : 0} reviews)</div>
                <div class="price">$${(product.price || 0).toFixed(2)}</div>
                <div class="description">${escHtml(product.description)}</div>

                <h4 style="font-size:14px;margin-bottom:8px">Stock by Region</h4>
                <div class="inventory-grid">
                    ${inventory && inventory.regionStock ? Object.entries(inventory.regionStock).map(([region, count]) => `
                        <div class="inventory-item">
                            <div class="region">${region}</div>
                            <div class="count" style="color:${count > 0 ? '#2e7d32' : '#c62828'}">${count}</div>
                        </div>
                    `).join('') : '<div class="inventory-item"><div class="region">N/A</div><div class="count">—</div></div>'}
                </div>

                <button class="add-to-cart-btn" onclick="addToCart('${product.id}')">
                    <i class="fas fa-cart-plus"></i> Add to Cart
                </button>
            </div>
        </div>

        ${product.reviews && product.reviews.length > 0 ? `
        <div class="reviews-section">
            <h2>All Reviews (${product.reviews.length})</h2>
            ${product.reviews.map(r => `
                <div class="review-card">
                    <div class="review-header">
                        <span class="reviewer"><i class="fas fa-user-circle"></i> ${escHtml(r.customerName)}</span>
                        <span class="date">${new Date(r.date).toLocaleDateString()}</span>
                    </div>
                    <div class="review-rating">${'★'.repeat(r.rating)}${'☆'.repeat(5 - r.rating)}</div>
                    <div class="review-title">${escHtml(r.title)}</div>
                    <div class="review-body">${escHtml(r.body)}</div>
                </div>
            `).join('')}
        </div>
        ` : ''}

        ${recProducts.length > 0 ? `
        <div class="recommendations">
            <div class="section-header">
                <h2>Customers Also Bought</h2>
            </div>
            <div class="product-grid">
                ${recProducts.map(p => productCard(p)).join('')}
            </div>
        </div>
        ` : ''}
    `;

    hideLoading();
}

// ============================================================
// ADD TO CART
// ============================================================
async function addToCart(productId) {
    showLoading('Adding to cart...');
    const result = await apiFetch(`/api/cart/${DEMO_CUSTOMER_ID}/items`, {
        method: 'POST',
        body: JSON.stringify({ productId: productId, quantity: 1 })
    });
    hideLoading();
    if (result) {
        showToast('Added to cart!');
        updateCartBadge(result);
    } else {
        showToast('Failed to add to cart');
    }
}

function updateCartBadge(cart) {
    const badge = document.getElementById('cartBadge');
    if (cart && cart.items && cart.items.length > 0) {
        badge.textContent = cart.items.length;
        badge.style.display = 'flex';
    } else {
        badge.style.display = 'none';
    }
}

// ============================================================
// CART — also starts polling every 2 seconds
// ============================================================
async function renderCart() {
    showLoading('Loading cart...');
    const cart = await apiFetch(`/api/cart/${DEMO_CUSTOMER_ID}`);
    hideLoading();

    const main = document.getElementById('mainContent');

    if (!cart || !cart.items || cart.items.length === 0) {
        main.innerHTML = `
            <div class="empty-cart">
                <i class="fas fa-shopping-cart"></i>
                <h2>Your cart is empty</h2>
                <p style="color:#aaa">Start shopping to add items!</p>
                <button class="hero-btn" style="margin-top:16px" onclick="navigateTo('home')">Continue Shopping</button>
            </div>
        `;
        startCartPolling();
        return;
    }

    // For each cart item, fetch the CURRENT product and inventory
    // (even though we already have a snapshot in the cart item)
    const enrichedItems = [];
    for (const item of cart.items) {
        const currentProduct = await apiFetch(`/api/products/${item.productId}`);
        const currentInv = await apiFetch(`/api/inventory/${item.productId}`);
        enrichedItems.push({ ...item, currentProduct, currentInv });
    }

    const subtotal = enrichedItems.reduce((s, i) => s + (i.priceAtAdd * i.quantity), 0);

    main.innerHTML = `
        <div class="section-header">
            <h2>Shopping Cart <span style="color:#999;font-weight:300">(${enrichedItems.length} items)</span></h2>
            <a onclick="navigateTo('home')"><i class="fas fa-arrow-left"></i> Continue Shopping</a>
        </div>
        <div class="cart-page">
            <div class="cart-items">
                ${enrichedItems.map(item => `
                    <div class="cart-item">
                        <div class="cart-item-img"><img src="/images/${slugify(item.productSnapshot?.name)}.svg" alt="" style="width:100%;height:100%;object-fit:contain" onerror="this.outerHTML='<i class=\'fas fa-cube\'></i>'"/></div>
                        <div class="cart-item-info">
                            <h3>${escHtml(item.productSnapshot?.name || 'Product')}</h3>
                            <div class="price">$${(item.priceAtAdd || 0).toFixed(2)}</div>
                            <div class="qty">Qty: ${item.quantity}</div>
                            <div style="font-size:11px;color:#888;margin-top:4px">
                                Current price: $${item.currentProduct ? item.currentProduct.price.toFixed(2) : '?'} ·
                                Stock: ${item.currentInv ? item.currentInv.totalStock : '?'}
                            </div>
                        </div>
                        <div class="cart-item-remove" onclick="removeFromCart('${item.itemId}')">
                            <i class="fas fa-trash"></i>
                        </div>
                    </div>
                `).join('')}
            </div>
            <div class="cart-summary">
                <h3>Order Summary</h3>
                <div class="cart-summary-row"><span>Subtotal</span><span>$${subtotal.toFixed(2)}</span></div>
                <div class="cart-summary-row"><span>Shipping</span><span>Free</span></div>
                <div class="cart-summary-row"><span>Tax</span><span>$${(subtotal * 0.08).toFixed(2)}</span></div>
                <div class="cart-summary-total"><span>Total</span><span>$${(subtotal * 1.08).toFixed(2)}</span></div>
                <button class="checkout-btn" onclick="checkout()">
                    <i class="fas fa-lock"></i> Checkout
                </button>
            </div>
        </div>
    `;

    // Start polling the cart every 2 seconds for "real-time updates"
    startCartPolling();
}

async function removeFromCart(itemId) {
    showLoading('Removing...');
    const cart = await apiFetch(`/api/cart/${DEMO_CUSTOMER_ID}/items/${itemId}`, { method: 'DELETE' });
    hideLoading();
    if (cart) {
        updateCartBadge(cart);
        renderCart();
    }
}

async function checkout() {
    showLoading('Processing checkout...');
    const order = await apiFetch(`/api/cart/${DEMO_CUSTOMER_ID}/checkout`, { method: 'POST' });
    hideLoading();
    if (order) {
        showToast(`Order ${order.id.slice(0, 8)}... placed!`);
        updateCartBadge(null);
        navigateTo('account');
    } else {
        showToast('Checkout failed');
    }
}

// ============================================================
// CART POLLING — polls every 2 seconds (per spec)
// ============================================================
function startCartPolling() {
    stopCartPolling();
    cartPollInterval = setInterval(async () => {
        const cart = await apiFetch(`/api/cart/${DEMO_CUSTOMER_ID}`);
        if (cart) updateCartBadge(cart);
    }, 2000);
}

function stopCartPolling() {
    if (cartPollInterval) {
        clearInterval(cartPollInterval);
        cartPollInterval = null;
    }
}

// ============================================================
// ACCOUNT — loads customer with full embedded order history
// ============================================================
async function renderAccount() {
    showLoading('Loading account...');

    // Load customer (which on the server: queries all orders, embeds them, replaces the doc)
    const customer = await apiFetch(`/api/customers/${DEMO_CUSTOMER_ID}`);

    // Also separately load orders (redundant — they're already in the customer doc now)
    const orders = await apiFetch(`/api/customers/${DEMO_CUSTOMER_ID}/orders`);

    // Also do an email lookup just to exercise that cross-partition query
    if (customer?.email) {
        await apiFetch(`/api/customers/search?email=${encodeURIComponent(customer.email)}`);
    }

    hideLoading();
    const main = document.getElementById('mainContent');

    if (!customer) {
        main.innerHTML = `
            <div class="account-page">
                <div class="account-header">
                    <div class="account-avatar">?</div>
                    <div class="account-info">
                        <h2>Demo Account</h2>
                        <p>Customer ID: ${DEMO_CUSTOMER_ID}</p>
                        <p style="color:#e94560; font-size:13px">Customer not found — seed data first via Admin panel</p>
                    </div>
                </div>
            </div>
        `;
        return;
    }

    main.innerHTML = `
        <div class="account-page">
            <div class="account-header">
                <div class="account-avatar">${(customer.firstName || '?')[0]}${(customer.lastName || '')[0]}</div>
                <div class="account-info">
                    <h2>${escHtml(customer.firstName)} ${escHtml(customer.lastName)}</h2>
                    <p>${escHtml(customer.email)}</p>
                </div>
            </div>

            <div class="account-section">
                <h3><i class="fas fa-map-marker-alt"></i> Addresses</h3>
                ${(customer.addresses || []).map(a => `
                    <div style="padding:8px 0;border-bottom:1px solid #f8f8f8">
                        <strong>${escHtml(a.label)}</strong> — ${escHtml(a.street)}, ${escHtml(a.city)}, ${escHtml(a.state)} ${escHtml(a.zipCode)}
                    </div>
                `).join('')}
            </div>

            <div class="account-section">
                <h3><i class="fas fa-credit-card"></i> Payment Methods</h3>
                ${(customer.paymentMethods || []).map(pm => `
                    <div style="padding:8px 0;border-bottom:1px solid #f8f8f8">
                        ${escHtml(pm.type)} ending in ${escHtml(pm.last4)} · Exp ${pm.expiryMonth}/${pm.expiryYear}
                        ${pm.isDefault ? '<span style="color:#e94560;font-size:12px;margin-left:8px">Default</span>' : ''}
                    </div>
                `).join('')}
            </div>

            <div class="account-section">
                <h3><i class="fas fa-box"></i> Order History (${(orders || customer.orderHistory || []).length} orders)</h3>
                ${(orders || customer.orderHistory || []).slice(0, 50).map(o => `
                    <div class="order-row">
                        <div>
                            <div class="order-id">#${(o.id || '').slice(0, 8)}...</div>
                            <div class="order-date">${new Date(o.orderDate).toLocaleDateString()}</div>
                        </div>
                        <span class="order-status ${(o.status || '').toLowerCase()}">${escHtml(o.status)}</span>
                        <span class="order-amount">$${(o.totalAmount || 0).toFixed(2)}</span>
                    </div>
                `).join('')}
            </div>
        </div>
    `;
}

// ============================================================
// ADMIN — seed, analytics (loads everything)
// ============================================================
async function renderAdmin() {
    const main = document.getElementById('mainContent');
    main.innerHTML = `
        <h2>Admin Dashboard</h2>

        <div class="admin-grid">
            <div class="admin-card">
                <h3>Seed Database</h3>
                <p style="color:#555;font-size:14px;margin-bottom:16px">
                    Create 10K products, 5K customers, 50K orders — sequentially, one at a time, at 400 RU/s.
                </p>
                <button class="admin-btn" id="seedBtn" onclick="runSeed()">
                    <i class="fas fa-database"></i> Seed Data
                </button>
                <div id="seedStatus" class="status-bar"></div>
            </div>
            <div class="admin-card">
                <h3>Rebuild Recommendations</h3>
                <p style="color:#555;font-size:14px;margin-bottom:16px">
                    Loads ALL orders into memory, builds co-purchase matrix, rewrites all recommendation documents.
                </p>
                <button class="admin-btn secondary" onclick="rebuildRecs()">
                    <i class="fas fa-brain"></i> Rebuild
                </button>
                <div id="recStatus" class="status-bar"></div>
            </div>
        </div>

        <h2 style="margin-top:32px">Analytics</h2>
        <div class="admin-grid">
            <div class="admin-card">
                <h3>Top Products (Last 30 Days)</h3>
                <button class="admin-btn" onclick="loadTopProducts()"><i class="fas fa-chart-bar"></i> Load</button>
                <div id="topProductsData"></div>
            </div>
            <div class="admin-card">
                <h3>Revenue by Category</h3>
                <button class="admin-btn" onclick="loadRevenue()"><i class="fas fa-dollar-sign"></i> Load</button>
                <div id="revenueData"></div>
            </div>
            <div class="admin-card">
                <h3>Most Active Customers</h3>
                <button class="admin-btn" onclick="loadActiveCustomers()"><i class="fas fa-users"></i> Load</button>
                <div id="activeCustomersData"></div>
            </div>
        </div>
    `;
}

async function runSeed() {
    const btn = document.getElementById('seedBtn');
    const status = document.getElementById('seedStatus');
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Seeding...';
    status.className = 'status-bar info';
    status.textContent = 'Seeding in progress — this will take a very long time at 400 RU/s...';

    const result = await apiFetch('/api/admin/seed', { method: 'POST' });

    btn.disabled = false;
    btn.innerHTML = '<i class="fas fa-database"></i> Seed Data';

    if (result) {
        status.className = 'status-bar success';
        status.textContent = `Seeded: ${result.products} products, ${result.customers} customers, ${result.orders} orders`;
    } else {
        status.className = 'status-bar error';
        status.textContent = 'Seeding failed — check server logs';
    }
}

async function rebuildRecs() {
    const status = document.getElementById('recStatus');
    status.className = 'status-bar info';
    status.textContent = 'Loading all orders into memory and rebuilding...';

    // Call a product endpoint which triggers rebuild (by design)
    const result = await apiFetch('/api/products/search?q=a');
    if (result && result.length > 0) {
        // Triggering rebuild by visiting a product detail page
        await apiFetch(`/api/products/${result[0].id}`);
        status.className = 'status-bar success';
        status.textContent = 'Recommendations rebuilt (triggered via product detail view)';
    } else {
        status.className = 'status-bar error';
        status.textContent = 'No products found — seed first';
    }
}

async function loadTopProducts() {
    const container = document.getElementById('topProductsData');
    container.innerHTML = '<p style="color:#888;font-size:13px"><i class="fas fa-spinner fa-spin"></i> Loading all orders and aggregating client-side...</p>';

    const data = await apiFetch('/api/admin/analytics/top-products');
    if (!data || data.length === 0) {
        container.innerHTML = '<p style="color:#888">No data — seed first</p>';
        return;
    }

    container.innerHTML = `
        <table class="analytics-table">
            <thead><tr><th>Product ID</th><th>Qty Sold</th><th>Revenue</th><th>Orders</th></tr></thead>
            <tbody>
                ${data.map(d => `<tr>
                    <td>${(d.productId || '').slice(0, 8)}...</td>
                    <td>${d.totalQuantity}</td>
                    <td>$${(d.totalRevenue || 0).toFixed(2)}</td>
                    <td>${d.orderCount}</td>
                </tr>`).join('')}
            </tbody>
        </table>
    `;
}

async function loadRevenue() {
    const container = document.getElementById('revenueData');
    container.innerHTML = '<p style="color:#888;font-size:13px"><i class="fas fa-spinner fa-spin"></i> Loading all orders...</p>';

    const data = await apiFetch('/api/admin/analytics/revenue');
    if (!data || data.length === 0) {
        container.innerHTML = '<p style="color:#888">No data</p>';
        return;
    }

    container.innerHTML = `
        <table class="analytics-table">
            <thead><tr><th>Category</th><th>Orders</th><th>Revenue</th></tr></thead>
            <tbody>
                ${data.map(d => `<tr>
                    <td>${escHtml(d.category)}</td>
                    <td>${d.total}</td>
                    <td>$${(d.revenue || 0).toFixed(2)}</td>
                </tr>`).join('')}
            </tbody>
        </table>
    `;
}

async function loadActiveCustomers() {
    const container = document.getElementById('activeCustomersData');
    container.innerHTML = '<p style="color:#888;font-size:13px"><i class="fas fa-spinner fa-spin"></i> Scanning ALL orders...</p>';

    const data = await apiFetch('/api/admin/analytics/active-customers');
    if (!data || data.length === 0) {
        container.innerHTML = '<p style="color:#888">No data</p>';
        return;
    }

    container.innerHTML = `
        <table class="analytics-table">
            <thead><tr><th>Customer</th><th>Orders</th><th>Total Spent</th><th>Last Order</th></tr></thead>
            <tbody>
                ${data.map(d => `<tr>
                    <td>${(d.customerId || '').slice(0, 8)}...</td>
                    <td>${d.orderCount}</td>
                    <td>$${(d.totalSpent || 0).toFixed(2)}</td>
                    <td>${new Date(d.lastOrder).toLocaleDateString()}</td>
                </tr>`).join('')}
            </tbody>
        </table>
    `;
}

// ============================================================
// Utility
// ============================================================
function escHtml(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

// ============================================================
// Init
// ============================================================
initPerfOverlay();
navigateTo('home');
