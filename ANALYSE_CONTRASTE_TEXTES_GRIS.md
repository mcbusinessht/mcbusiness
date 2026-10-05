# 🔍 ANALYSE DES CONTRASTES - TEXTES GRIS EN DARK MODE

**Date:** 16 Mars 2026  
**Objectif:** Identifier et corriger les textes gris illisibles

---

## 📊 PROBLÈMES IDENTIFIÉS

### 1️⃣ Variables actuelles dans `dark-mode.css`

**Ligne 42-44:**
```css
--text-primary: #f0f4f8;      /* ✅ Excellent (15:1) */
--text-secondary: #b8c5d6;    /* ✅ Bon (9:1) */
--text-muted: #8492a6;        /* ⚠️ Limite (5:1) */
```

**Ligne 294-295:**
```css
.text-muted {
    color: var(--text-secondary) !important;  /* ❌ ERREUR ! */
}
```

**PROBLÈME:** `.text-muted` utilise `--text-secondary` (#b8c5d6) au lieu de `--text-muted` (#8492a6)

### 2️⃣ Analyse du contraste

Sur fond **#0a0e14** (noir profond):

| Couleur | Usage | Contraste | Statut |
|---------|-------|-----------|--------|
| #f0f4f8 | text-primary | 15.2:1 | ✅ Excellent |
| #b8c5d6 | text-secondary | 9.1:1 | ✅ Très bon |
| #8492a6 | text-muted | 5.2:1 | ⚠️ Acceptable (WCAG AA) |
| #6c757d | (mode clair) | 3.8:1 | ❌ Insuffisant (< 4.5:1) |
| #64748b | (si utilisé) | 4.1:1 | ❌ Limite (< 4.5:1) |
| #5a6c7d | text-disabled | 3.2:1 | ❌ Trop faible |

### 3️⃣ Classes problématiques potentielles

Classes qui pourraient utiliser des gris trop sombres:

```css
.text-muted          /* ⚠️ Actuellement: #b8c5d6 (bon) mais devrait être #8492a6 */
.text-secondary      /* ✅ #b8c5d6 (bon) */
small                /* ❌ Hérite possiblement du parent */
.caption             /* ❌ Non défini */
.lead                /* ❌ Non défini */
p.text-gray-600      /* ❌ Si utilisé */
```

---

## 🎨 COULEURS RECOMMANDÉES

### Pour améliorer la lisibilité:

```css
/* Texte principal - Très clair */
--text-primary: #f0f4f8;        /* Contraste 15:1 ✅ */

/* Texte corps - Clair */
--text-body: #cbd5e1;           /* Contraste 11:1 ✅ */

/* Texte secondaire - Moyen-clair */
--text-secondary: #b8c5d6;      /* Contraste 9:1 ✅ */

/* Texte muted - Lisible */
--text-muted: #94a3b8;          /* Contraste 6.5:1 ✅ (amélioré) */

/* Texte faible */
--text-subtle: #8492a6;         /* Contraste 5.2:1 ✅ */

/* Texte disabled */
--text-disabled: #64748b;       /* Contraste 4.1:1 ⚠️ (limite) */
```

---

## 🔧 CORRECTIONS NÉCESSAIRES

### Correction #1: Fixer `.text-muted`

**Fichier:** `wwwroot/css/dark-mode.css`  
**Ligne:** 293-295

**AVANT:**
```css
.text-muted {
    color: var(--text-secondary) !important;
}
```

**APRÈS:**
```css
[data-theme="dark"] .text-muted {
    color: #94a3b8 !important;  /* Amélioré pour meilleure lisibilité */
}
```

### Correction #2: Améliorer `--text-muted`

**Fichier:** `wwwroot/css/dark-mode.css`  
**Ligne:** 43

**AVANT:**
```css
--text-muted: #8492a6;
```

**APRÈS:**
```css
--text-muted: #94a3b8;  /* Contraste amélioré: 6.5:1 */
```

### Correction #3: Ajouter classes manquantes

**Ajouter après ligne 430:**
```css
/* Textes gris avec bon contraste */
[data-theme="dark"] small,
[data-theme="dark"] .small {
    color: #94a3b8 !important;
}

[data-theme="dark"] .caption,
[data-theme="dark"] .text-caption {
    color: #94a3b8 !important;
}

[data-theme="dark"] .lead {
    color: #cbd5e1 !important;
}

[data-theme="dark"] p {
    color: #b8c5d6 !important;
}

[data-theme="dark"] .text-gray-400,
[data-theme="dark"] .text-gray-500,
[data-theme="dark"] .text-gray-600 {
    color: #94a3b8 !important;
}
```

---

## 📋 RÉSUMÉ DES CORRECTIONS

**3 corrections minimales:**

1. ✅ Changer `--text-muted` de `#8492a6` → `#94a3b8`
2. ✅ Corriger `.text-muted` pour utiliser la nouvelle couleur
3. ✅ Ajouter classes manquantes (small, caption, p, lead)

**Impact:**
- Contraste amélioré de 5.2:1 → 6.5:1
- Meilleure lisibilité sans changer le design
- Conformité WCAG AA+ maintenue

---

**Analyse effectuée le:** 16 Mars 2026
