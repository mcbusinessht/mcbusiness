# 🌙 CONCEPTION DARK MODE PROFESSIONNEL - MC BUSINESS (PARTIE 2)

**Date:** 16 Mars 2026  
**Suite de:** DARK_MODE_CONCEPTION_PARTIE1.md

---

## 🏗️ PARTIE 4: ARCHITECTURE CSS AVEC VARIABLES

### Structure recommandée

```css
/* ============================================
   ARCHITECTURE CSS VARIABLES - MC BUSINESS
   ============================================ */

:root {
    /* === COULEURS BRANDING (Mode Clair) === */
    --mcb-primary: #005fac;
    --mcb-secondary: #5dab31;
    
    /* === COULEURS SYSTÈME (Mode Clair) === */
    --color-bg-body: #ffffff;
    --color-bg-surface: #f8f9fa;
    --color-bg-elevated: #ffffff;
    --color-bg-hover: #e9ecef;
    
    --color-text-primary: #212529;
    --color-text-secondary: #6c757d;
    --color-text-muted: #adb5bd;
    
    --color-border: #dee2e6;
    --color-border-hover: #adb5bd;
    
    --color-primary: #005fac;
    --color-primary-hover: #004c89;
    --color-secondary: #5dab31;
    --color-secondary-hover: #4a8827;
    
    /* === OMBRES === */
    --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.05);
    --shadow-md: 0 4px 12px rgba(0, 0, 0, 0.1);
    --shadow-lg: 0 8px 24px rgba(0, 0, 0, 0.15);
    
    /* === TRANSITIONS === */
    --transition-fast: 0.15s ease;
    --transition-normal: 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    --transition-slow: 0.5s cubic-bezier(0.4, 0, 0.2, 1);
}

/* ============================================
   MODE SOMBRE [data-theme="dark"]
   ============================================ */

[data-theme="dark"] {
    /* === ARRIÈRE-PLANS === */
    --color-bg-body: #0a0e14;
    --color-bg-surface: #151b23;
    --color-bg-elevated: #1e2732;
    --color-bg-hover: #2a3441;
    
    /* === TEXTES === */
    --color-text-primary: #f0f4f8;
    --color-text-secondary: #b8c5d6;
    --color-text-muted: #8492a6;
    
    /* === BORDURES === */
    --color-border: rgba(26, 140, 255, 0.15);
    --color-border-hover: rgba(26, 140, 255, 0.3);
    
    /* === COULEURS PRIMAIRES ADAPTÉES === */
    --color-primary: #1a8cff;           /* Bleu MCBusiness éclairci */
    --color-primary-hover: #0066cc;
    --color-primary-rgb: 26, 140, 255;
    
    --color-secondary: #7ed957;         /* Vert MCBusiness éclairci */
    --color-secondary-hover: #5dab31;
    --color-secondary-rgb: 126, 217, 87;
    
    /* === OMBRES SOMBRES === */
    --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.5);
    --shadow-md: 0 4px 12px rgba(0, 0, 0, 0.4);
    --shadow-lg: 0 8px 24px rgba(0, 0, 0, 0.5);
    --shadow-glow-primary: 0 0 20px rgba(26, 140, 255, 0.3);
    --shadow-glow-secondary: 0 0 20px rgba(126, 217, 87, 0.3);
    
    /* === GLASSMORPHISM === */
    --glass-bg: rgba(21, 27, 35, 0.7);
    --glass-border: rgba(26, 140, 255, 0.2);
    --glass-blur: blur(20px) saturate(180%);
}

/* ============================================
   UTILISATION DANS LES COMPOSANTS
   ============================================ */

/* Corps de la page */
body {
    background-color: var(--color-bg-body);
    color: var(--color-text-primary);
    transition: background-color var(--transition-normal),
                color var(--transition-normal);
}

/* Surfaces (Cards, Panels) */
.card, .panel, .surface {
    background-color: var(--color-bg-surface);
    border: 1px solid var(--color-border);
    box-shadow: var(--shadow-md);
    transition: all var(--transition-normal);
}

.card:hover {
    background-color: var(--color-bg-hover);
    border-color: var(--color-border-hover);
    box-shadow: var(--shadow-lg);
}

/* Boutons Primary */
.btn-primary {
    background: var(--color-primary);
    color: white;
    border: none;
    transition: all var(--transition-normal);
}

.btn-primary:hover {
    background: var(--color-primary-hover);
}

[data-theme="dark"] .btn-primary {
    background: linear-gradient(135deg, var(--color-primary), var(--color-primary-hover));
    box-shadow: var(--shadow-glow-primary);
}

/* Textes */
.text-primary-custom {
    color: var(--color-text-primary);
}

.text-secondary-custom {
    color: var(--color-text-secondary);
}

.text-muted-custom {
    color: var(--color-text-muted);
}

/* Bordures */
.border-custom {
    border-color: var(--color-border);
}

.border-custom:hover {
    border-color: var(--color-border-hover);
}
```

---

## ⚙️ PARTIE 5: CORRECTIONS CSS CONCRÈTES

### 1️⃣ NAVBAR - Header principale

**Fichier à modifier:** `wwwroot/css/dark-mode.css`

**Ajout:**

```css
/* === NAVBAR PRINCIPALE === */
[data-theme="dark"] .navbar.bg-white {
    background: rgba(21, 27, 35, 0.95) !important;
    backdrop-filter: blur(20px) saturate(180%);
    border-bottom-color: rgba(26, 140, 255, 0.2) !important;
}

[data-theme="dark"] .navbar-light .navbar-brand,
[data-theme="dark"] .navbar-light .nav-link {
    color: #f0f4f8 !important;
}

[data-theme="dark"] .navbar-light .nav-link:hover {
    color: #1a8cff !important;
}

/* Header top row (announcement bar) */
[data-theme="dark"] .header-top-row.bg-primary {
    background: linear-gradient(90deg, #0a0e14 0%, #151b23 50%, #0a0e14 100%) !important;
    border-bottom: 1px solid rgba(26, 140, 255, 0.3);
}

/* Search bar dans navbar */
[data-theme="dark"] .navbar .form-control {
    background: rgba(21, 27, 35, 0.6) !important;
    border-color: rgba(26, 140, 255, 0.2) !important;
    color: #f0f4f8 !important;
}

[data-theme="dark"] .navbar .form-control:focus {
    border-color: #1a8cff !important;
    box-shadow: 0 0 0 3px rgba(26, 140, 255, 0.15) !important;
}
```

---

### 2️⃣ HERO SECTION

**Fichier à modifier:** `Pages/Shared/_IndexStyles.cshtml` ou nouveau fichier

```css
/* === HERO SECTION DARK MODE === */
[data-theme="dark"] .hero-section {
    background: linear-gradient(135deg, #0a0e14 0%, #151b23 100%) !important;
}

[data-theme="dark"] .hero-title {
    color: #f0f4f8 !important;
}

[data-theme="dark"] .hero-title .text-primary {
    color: #1a8cff !important;
    text-shadow: 0 0 30px rgba(26, 140, 255, 0.3);
}

[data-theme="dark"] .hero-subtitle {
    color: #b8c5d6 !important;
}

[data-theme="dark"] .hero-features .feature {
    color: #f0f4f8 !important;
}

[data-theme="dark"] .hero-features .feature i {
    filter: drop-shadow(0 0 10px rgba(26, 140, 255, 0.5));
}

/* Floating cards dans hero */
[data-theme="dark"] .floating-card {
    background: rgba(21, 27, 35, 0.9) !important;
    border: 1px solid rgba(26, 140, 255, 0.3) !important;
    backdrop-filter: blur(20px);
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5),
                0 0 20px rgba(26, 140, 255, 0.2);
}
```

---

### 3️⃣ PRODUCT CARDS

**Fichier à modifier:** `Pages/Shared/_IndexStyles.cshtml`

```css
/* === PRODUCT CARDS DARK MODE === */
[data-theme="dark"] .products-section {
    background: #0a0e14 !important;
}

[data-theme="dark"] .product-card {
    background: rgba(21, 27, 35, 0.8) !important;
    border: 1px solid rgba(26, 140, 255, 0.15) !important;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
    transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

[data-theme="dark"] .product-card:hover {
    background: rgba(30, 39, 50, 0.9) !important;
    border-color: rgba(26, 140, 255, 0.3) !important;
    transform: translateY(-6px);
    box-shadow: 0 12px 36px rgba(0, 0, 0, 0.6),
                0 0 24px rgba(26, 140, 255, 0.25);
}

[data-theme="dark"] .product-name {
    color: #f0f4f8 !important;
}

[data-theme="dark"] .product-category small {
    color: #8492a6 !important;
}

[data-theme="dark"] .product-price .price-current {
    color: #1a8cff !important;
    text-shadow: 0 0 10px rgba(26, 140, 255, 0.3);
}

[data-theme="dark"] .product-price .price-old {
    color: #8492a6 !important;
}

/* Product badges */
[data-theme="dark"] .product-badge.sale {
    background: linear-gradient(135deg, #ef4444, #dc2626) !important;
    box-shadow: 0 0 15px rgba(239, 68, 68, 0.5);
}

[data-theme="dark"] .product-badge.new {
    background: linear-gradient(135deg, #7ed957, #5dab31) !important;
    box-shadow: 0 0 15px rgba(126, 217, 87, 0.5);
}

/* Product overlay */
[data-theme="dark"] .product-overlay {
    background: rgba(10, 14, 20, 0.95) !important;
}

/* Stock indicators */
[data-theme="dark"] .product-stock .text-success {
    color: #22c55e !important;
}

[data-theme="dark"] .product-stock .text-warning {
    color: #f59e0b !important;
}

[data-theme="dark"] .product-stock .text-danger {
    color: #ef4444 !important;
}
```

---

### 4️⃣ PLAN CARDS

```css
/* === PLAN CARDS DARK MODE === */
[data-theme="dark"] .plans-section {
    background: #151b23 !important;
}

[data-theme="dark"] .plan-card {
    background: rgba(21, 27, 35, 0.8) !important;
    border: 1px solid rgba(26, 140, 255, 0.2) !important;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.4);
    transition: all 0.3s ease;
}

[data-theme="dark"] .plan-card:hover {
    background: rgba(30, 39, 50, 0.9) !important;
    border-color: rgba(26, 140, 255, 0.4) !important;
    transform: translateY(-8px) scale(1.02);
    box-shadow: 0 16px 48px rgba(0, 0, 0, 0.6),
                0 0 30px rgba(26, 140, 255, 0.3);
}

[data-theme="dark"] .plan-name {
    color: #f0f4f8 !important;
}

[data-theme="dark"] .plan-price {
    color: #1a8cff !important;
    text-shadow: 0 0 10px rgba(26, 140, 255, 0.3);
}

[data-theme="dark"] .plan-features li {
    color: #b8c5d6 !important;
}

[data-theme="dark"] .plan-features .text-success {
    color: #22c55e !important;
    filter: drop-shadow(0 0 5px rgba(34, 197, 94, 0.5));
}
```

---

### 5️⃣ WHY US SECTION

```css
/* === WHY US SECTION DARK MODE === */
[data-theme="dark"] .why-choose-us-section.bg-white {
    background: #0a0e14 !important;
}

[data-theme="dark"] .section-header .section-subtitle {
    color: #1a8cff !important;
}

[data-theme="dark"] .section-header .section-title {
    color: #f0f4f8 !important;
}

[data-theme="dark"] .section-header .section-description {
    color: #b8c5d6 !important;
}

[data-theme="dark"] .why-card {
    background: rgba(21, 27, 35, 0.6) !important;
    border: 1px solid rgba(26, 140, 255, 0.15) !important;
    backdrop-filter: blur(10px);
    transition: all 0.3s ease;
}

[data-theme="dark"] .why-card:hover {
    background: rgba(30, 39, 50, 0.8) !important;
    border-color: rgba(26, 140, 255, 0.3) !important;
    transform: translateY(-4px);
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.5),
                0 0 20px rgba(26, 140, 255, 0.2);
}

[data-theme="dark"] .why-title {
    color: #f0f4f8 !important;
}

[data-theme="dark"] .why-text {
    color: #b8c5d6 !important;
}

/* Icônes colorées gardent leurs couleurs mais avec glow */
[data-theme="dark"] .why-icon i.text-success {
    filter: drop-shadow(0 0 10px rgba(34, 197, 94, 0.5));
}

[data-theme="dark"] .why-icon i.text-warning {
    filter: drop-shadow(0 0 10px rgba(245, 158, 11, 0.5));
}

[data-theme="dark"] .why-icon i.text-primary {
    filter: drop-shadow(0 0 10px rgba(26, 140, 255, 0.5));
}
```

---

### 6️⃣ STATS SECTION

```css
/* === STATS SECTION DARK MODE === */
[data-theme="dark"] .stats-section {
    background: #151b23 !important;
}

[data-theme="dark"] .stat-card {
    background: rgba(21, 27, 35, 0.8) !important;
    border: 1px solid rgba(26, 140, 255, 0.2) !important;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.4);
    transition: all 0.3s ease;
}

[data-theme="dark"] .stat-card:hover {
    background: rgba(30, 39, 50, 0.9) !important;
    border-color: rgba(26, 140, 255, 0.4) !important;
    transform: translateY(-4px) scale(1.05);
    box-shadow: 0 12px 32px rgba(0, 0, 0, 0.6),
                0 0 25px rgba(26, 140, 255, 0.3);
}

[data-theme="dark"] .stat-icon {
    color: #1a8cff !important;
    filter: drop-shadow(0 0 15px rgba(26, 140, 255, 0.5));
}

[data-theme="dark"] .stat-value {
    color: #f0f4f8 !important;
    text-shadow: 0 0 20px rgba(26, 140, 255, 0.3);
}

[data-theme="dark"] .stat-label {
    color: #b8c5d6 !important;
}
```

---

### 7️⃣ FOOTER

```css
/* === FOOTER DARK MODE === */
[data-theme="dark"] footer.bg-primary {
    background: linear-gradient(135deg, #0a0e14 0%, #151b23 100%) !important;
    border-top: 2px solid rgba(26, 140, 255, 0.3);
}

[data-theme="dark"] footer h5,
[data-theme="dark"] footer h6 {
    color: #1a8cff !important;
}

[data-theme="dark"] footer p,
[data-theme="dark"] footer li {
    color: #b8c5d6 !important;
}

[data-theme="dark"] footer a {
    color: #b8c5d6 !important;
    transition: all 0.3s ease;
}

[data-theme="dark"] footer a:hover {
    color: #1a8cff !important;
    text-shadow: 0 0 10px rgba(26, 140, 255, 0.5);
}

/* Social icons */
[data-theme="dark"] footer .bi {
    filter: drop-shadow(0 0 5px rgba(26, 140, 255, 0.3));
    transition: filter 0.3s ease;
}

[data-theme="dark"] footer .bi:hover {
    filter: drop-shadow(0 0 15px rgba(26, 140, 255, 0.7));
}
```

---

### 8️⃣ BADGES

```css
/* === BADGES DARK MODE === */
[data-theme="dark"] .badge.bg-success {
    background: linear-gradient(135deg, #22c55e, #16a34a) !important;
    box-shadow: 0 0 10px rgba(34, 197, 94, 0.5);
}

[data-theme="dark"] .badge.bg-primary {
    background: linear-gradient(135deg, #1a8cff, #0066cc) !important;
    box-shadow: 0 0 10px rgba(26, 140, 255, 0.5);
}

[data-theme="dark"] .badge.bg-warning {
    background: linear-gradient(135deg, #f59e0b, #d97706) !important;
    box-shadow: 0 0 10px rgba(245, 158, 11, 0.5);
    color: #0a0e14 !important; /* Texte sombre pour contraste */
}
```

---

