// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// 

// JS pour la side bar de Supplier
    document.addEventListener('DOMContentLoaded', function() {
        // Toggle sidebar groups
        document.querySelectorAll('.sidebar-group').forEach(group => {
            const item = group.querySelector('.sidebar-item');
            const submenu = group.querySelector('.sidebar-submenu');
            
            // Open if contains active item
            if (submenu.querySelector('.active')) {
                group.classList.add('active');
            }
            
            item.addEventListener('click', function(e) {
                if (e.target.tagName === 'A') return;
                group.classList.toggle('active');
            });
        });
        
        // Toggle sidebar subgroups
        document.querySelectorAll('.sidebar-subgroup').forEach(group => {
            const item = group.querySelector('.sidebar-subitem');
            const submenu = group.querySelector('.sidebar-subsubmenu');
            
            // Open if contains active item
            if (submenu.querySelector('.active')) {
                group.classList.add('active');
            }
            
            item.addEventListener('click', function(e) {
                if (e.target.tagName === 'A') return;
                group.classList.toggle('active');
            });
        });
    });

//Recaptcha and Bootstrap tooltips initialization
document.addEventListener('DOMContentLoaded', function() {
    // Bootstrap tooltips
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl));
});

window.onload = function() {
    // Vérifier si la connexion est stable au moment où la page est complètement chargée
    if (navigator.onLine) {
        // Si la connexion est stable, masquer l'animation et afficher le contenu
        document.getElementById("loading-screen").style.display = "none";
        document.getElementById("content").style.display = "block";
    } else {
        // Si la connexion est instable ou absente, afficher l'animation de chargement
        document.getElementById("loading-screen").style.display = "flex";
        document.getElementById("content").style.display = "none";

        // Ajouter un écouteur d'événement pour détecter lorsque la connexion devient disponible
        window.addEventListener("online", function() {
            // Lorsque la connexion devient disponible, masquer l'animation et afficher le contenu
            document.getElementById("loading-screen").style.display = "none";
            document.getElementById("content").style.display = "block";
        });
    }
};
document.addEventListener("DOMContentLoaded", function () {
    const elements = document.querySelectorAll(".hidden");

    // Fonction pour vérifier si l'élément est visible dans la fenêtre
    const isVisible = (element) => {
        const rect = element.getBoundingClientRect();
        return rect.top <= window.innerHeight && rect.bottom >= 0;
    };

    // Appliquer l'effet lors du défilement
    const handleScroll = () => {
        elements.forEach((element) => {
            if (isVisible(element)) {
                element.classList.add("show");
            }
        });
    };

    // Gestion de l'inactivité
    let timeout;

    const resetTimer = () => {
        clearTimeout(timeout);
        timeout = setTimeout(logoutUser, 20 * 60 * 1000); // 10 minutes en millisecondes
    };

    const logoutUser = () => {
        //alert("Vous avez été déconnecté pour inactivité.");
        window.location.href = '/Account/Login?handler=LogOut'; // Appelle OnGetLogOut
    };

    // Gestion combinée des événements
    const handleUserActivity = () => {
        resetTimer(); // Réinitialise le timer d'inactivité
        handleScroll(); // Applique les effets de défilement
    };

    // Ajouter les écouteurs d'événements
    window.addEventListener("scroll", handleUserActivity);
    document.addEventListener("mousemove", resetTimer);
    document.addEventListener("keypress", resetTimer);
    document.addEventListener("click", resetTimer);

    // Lancer une première vérification au chargement
    handleScroll();
    resetTimer();
});

function scrollLeft() {
    document.getElementById('scroll-container').scrollBy({ left: -100, behavior: 'smooth' });
}
function scrollRight() {
    document.getElementById('scroll-container').scrollBy({ left: 300, behavior: 'smooth' });
}


fetch("/api/timezone", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(Intl.DateTimeOffsetFormat().resolvedOptions().timeZone)
});

//-----------------------------------------------------------------------------------------------

// Données du graphique directement en Razor
    console.log("Labels:", labels);
    console.log("Values:", values);

    const GainChart = document.getElementById('GainChart').getContext('2d');
    new Chart(GainChart, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Gain Mensuel',
                data: values,
                backgroundColor: 'rgba(54, 162, 235, 0.5)',
                borderColor: 'rgba(54, 162, 235, 1)',
                borderWidth: 1,         
                tension: 0.3 // 🎯 Rend la courbe plus lissée
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false, // Prevents stretching
            scales: {
                y: { beginAtZero: true }
            }
        }
    });
const FollowersChart = document.getElementById('FollowersChart').getContext('2d');
new Chart(FollowersChart, {
    type: 'bar',
    data: {
        labels: labels,
        datasets: [{
            label: 'Followers Mensuel',
            data: values,
            backgroundColor: 'rgba(54, 162, 235, 0.5)',
            borderColor: 'rgba(54, 162, 235, 1)',
            borderWidth: 1
        }]
    },
    options: {
        responsive: true,
        maintainAspectRatio: false, // Prevents stretching
        scales: {
            y: { beginAtZero: true }
        }
    }
});

function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    sidebar.classList.toggle('active');
}

window.history.pushState(null, "", window.location.href);
window.onpopstate = function () {
    window.history.pushState(null, "", window.location.href);
};

function copyText() {
    var copyText = document.getElementById("textToShare");

    // Vérifier si l'API clipboard est disponible
    if (navigator.clipboard) {
        navigator.clipboard.writeText(copyText.value)
            .then(() => {
                alert("Texte copié : " + copyText.value);
            })
            .catch(err => {
                console.error("Erreur lors de la copie : ", err);
                alert("Impossible de copier le texte");
            });
    } else {
        // Solution de secours pour les anciens navigateurs
        copyText.select();
        copyText.setSelectionRange(0, 99999);
        document.execCommand("copy");
        alert("Texte copié : " + copyText.value);
    }
}

function shareOnPlatform(platform) {
    var text = document.getElementById("textToShare").value;
    var url;

    switch (platform) {
        case 'facebook':
            url = `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(text)}`;
            break;
        case 'twitter':
            url = `https://twitter.com/intent/tweet?text=${encodeURIComponent(text)}`;
            break;
        case 'whatsapp':
            url = `https://wa.me/?text=${encodeURIComponent(text)}`;
            break;
        default:
            alert('Plateforme non supportée');
            return;
    }

    // Ouvrir l'URL de partage dans une nouvelle fenêtre
    window.open(url, '_blank');
}

//------------------------------------------------------------------------------------
//Add Caracteristique
