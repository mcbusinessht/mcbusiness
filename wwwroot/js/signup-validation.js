// ===== GESTION DES SECTIONS PROGRESSIVES =====
function showSection1() {
    document.getElementById('section1').style.display = 'block';
    document.getElementById('section2').style.display = 'none';
    document.getElementById('section3').style.display = 'none';
    document.getElementById('sectionFinal').style.display = 'none';
}

function showSection2() {
    // Valider Section 1 avant de passer à Section 2
    if (!validateSection1()) {
        return;
    }

    document.getElementById('section1').style.display = 'none';
    document.getElementById('section2').style.display = 'block';
    document.getElementById('section3').style.display = 'none';
    document.getElementById('sectionFinal').style.display = 'none';
}

function proceedToFinal() {
    // Valider Section 2 avant de finaliser
    if (!validateSection2()) {
        return;
    }

    document.getElementById('section1').style.display = 'none';
    document.getElementById('section2').style.display = 'none';
    document.getElementById('section3').style.display = 'none';
    document.getElementById('sectionFinal').style.display = 'block';
}

function toggleReferralSection() {
    const section3 = document.getElementById('section3');
    const isVisible = section3.style.display !== 'none';
    
    if (isVisible) {
        // Masquer la section parrainage et revenir à section 2
        section3.style.display = 'none';
        document.getElementById('section2').style.display = 'block';
    } else {
        // Afficher la section parrainage
        document.getElementById('section2').style.display = 'none';
        section3.style.display = 'block';
    }
}

// ===== VALIDATION PROGRESSIVE =====
function validateSection1() {
    const email = document.querySelector('input[name="Input.Email"]').value;
    const phone = document.querySelector('input[name="Input.PhoneNumber"]').value;
    const password = document.querySelector('input[name="Input.Password"]').value;
    const confirmPassword = document.querySelector('input[name="Input.ConfirmPassword"]').value;
    const codeNumber = document.querySelector('select[name="Input.CodeNumber"]').value;

    let errors = [];

    // Validation email
    if (!email || email.indexOf('@') === -1) {
        errors.push('Veuillez saisir une adresse e-mail valide.');
        document.querySelector('input[name="Input.Email"]').classList.add('is-invalid');
    } else {
        document.querySelector('input[name="Input.Email"]').classList.remove('is-invalid');
    }

    // Validation téléphone et code pays
    if (!phone || !codeNumber) {
        errors.push('Veuillez saisir un numéro de téléphone complet avec le code pays.');
        document.querySelector('input[name="Input.PhoneNumber"]').classList.add('is-invalid');
    } else {
        document.querySelector('input[name="Input.PhoneNumber"]').classList.remove('is-invalid');
    }

    // Validation force du mot de passe
    if (!isPasswordStrong(password)) {
        errors.push('Le mot de passe doit respecter tous les critères de sécurité (8 caractères, majuscule, minuscule, chiffre, caractère spécial).');
        document.querySelector('input[name="Input.Password"]').classList.add('is-invalid');
    } else {
        document.querySelector('input[name="Input.Password"]').classList.remove('is-invalid');
    }

    // Validation correspondance mot de passe
    if (password !== confirmPassword) {
        errors.push('La confirmation du mot de passe ne correspond pas.');
        document.querySelector('input[name="Input.ConfirmPassword"]').classList.add('is-invalid');
        document.getElementById('confirmPasswordError').textContent = 'Les mots de passe ne correspondent pas.';
    } else if (confirmPassword) {
        document.querySelector('input[name="Input.ConfirmPassword"]').classList.remove('is-invalid');
        document.getElementById('confirmPasswordError').textContent = '';
    }

    if (errors.length > 0) {
        alert(errors.join('\n\n'));
        return false;
    }

    return true;
}

function validateSection2() {
    const firstName = document.querySelector('input[name="Input.FirstName"]').value;
    const lastName = document.querySelector('input[name="Input.LastName"]').value;
    
    let errors = [];

    if (!firstName.trim()) {
        errors.push('Veuillez saisir votre prénom.');
        document.querySelector('input[name="Input.FirstName"]').classList.add('is-invalid');
    } else {
        document.querySelector('input[name="Input.FirstName"]').classList.remove('is-invalid');
    }

    if (!lastName.trim()) {
        errors.push('Veuillez saisir votre nom.');
        document.querySelector('input[name="Input.LastName"]').classList.add('is-invalid');
    } else {
        document.querySelector('input[name="Input.LastName"]').classList.remove('is-invalid');
    }

    if (errors.length > 0) {
        alert(errors.join('\n\n'));
        return false;
    }

    return true;
}

// ===== GESTION CODE PARRAINAGE DEPUIS URL =====
function handleReferralCodeFromURL() {
    const urlParams = new URLSearchParams(window.location.search);
    const referralCode = urlParams.get('referralCode') || urlParams.get('ref') || urlParams.get('code');
    
    if (referralCode) {
        document.getElementById('referrerC').value = referralCode;
        console.log('Code de parrainage détecté depuis URL:', referralCode);
    }
}

// Validation finale lors de la soumission du formulaire
function validateFinalForm() {
    // Vérifier que toutes les sections sont valides
    if (!validateSection1() || !validateSection2()) {
        alert('Veuillez compléter toutes les informations requises.');
        return false;
    }
    
    // Validation reCAPTCHA
    const recaptchaResponse = grecaptcha.getResponse();
    if (!recaptchaResponse) {
        alert('Veuillez valider le reCAPTCHA.');
        return false;
    }
    
    // Validation termes et conditions
    const termsAccepted = document.getElementById('TM').checked;
    if (!termsAccepted) {
        alert('Veuillez accepter les termes et conditions.');
        return false;
    }
    
    return true;
}

// ---- Vérification mot de passe fort ----
function isPasswordStrong(password) {
    return password.length >= 8 && 
           /[a-z]/.test(password) && 
           /[A-Z]/.test(password) && 
           /\d/.test(password) &&
           /[^a-zA-Z0-9]/.test(password);
}

// ---- Validation correspondance mot de passe en temps réel ----
function validatePasswordMatch() {
    const password = document.getElementById("password").value;
    const confirmPassword = document.getElementById("confirmPassword").value;
    const errorDiv = document.getElementById('confirmPasswordError');
    const confirmField = document.getElementById("confirmPassword");

    if (confirmPassword && password !== confirmPassword) {
        errorDiv.textContent = '❌ Les mots de passe ne correspondent pas.';
        errorDiv.style.color = '#dc3545';
        confirmField.classList.add('is-invalid');
    } else if (confirmPassword && password === confirmPassword) {
        errorDiv.textContent = '✅ Les mots de passe correspondent.';
        errorDiv.style.color = '#198754';
        confirmField.classList.remove('is-invalid');
    } else {
        errorDiv.textContent = '';
        confirmField.classList.remove('is-invalid');
    }
}