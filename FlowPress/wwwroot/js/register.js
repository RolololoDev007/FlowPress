document.addEventListener("DOMContentLoaded", function () {
    // Toggle para Password
    const togglePasswordBtn = document.querySelector("#togglePassword");
    const passwordInput = document.querySelector("#RegisterModel_Password");
    const passwordIcon = togglePasswordBtn?.querySelector("span");

    if (togglePasswordBtn && passwordInput && passwordIcon) {
        togglePasswordBtn.addEventListener("click", function () {
            const type = passwordInput.getAttribute("type") === "password" ? "text" : "password";
            passwordInput.setAttribute("type", type);
            passwordIcon.textContent = type === "password" ? "visibility" : "visibility_off";
        });
    }

    // Toggle para Confirm Password
    const toggleConfirmBtn = document.querySelector("#toggleConfirmPassword");
    const confirmPasswordInput = document.querySelector("#RegisterModel_ConfirmPassword");
    const confirmIcon = toggleConfirmBtn?.querySelector("span");

    if (toggleConfirmBtn && confirmPasswordInput && confirmIcon) {
        toggleConfirmBtn.addEventListener("click", function () {
            const type = confirmPasswordInput.getAttribute("type") === "password" ? "text" : "password";
            confirmPasswordInput.setAttribute("type", type);
            confirmIcon.textContent = type === "password" ? "visibility" : "visibility_off";
        });
    }

    // Barra de fortaleza de contraseña
    const strengthBar = document.querySelector("#passwordStrengthBar");
    const strengthText = document.querySelector("#passwordStrengthText");

    if (passwordInput && strengthBar && strengthText) {
        passwordInput.addEventListener("input", function () {
            const value = passwordInput.value;
            let strength = 0;

            if (value.length >= 6) strength += 1;
            if (/[A-Z]/.test(value)) strength += 1;
            if (/[a-z]/.test(value)) strength += 1;
            if (/[0-9]/.test(value)) strength += 1;
            if (/[^A-Za-z0-9]/.test(value)) strength += 1;

            const percentage = (strength / 5) * 100;
            strengthBar.style.width = percentage + "%";

            if (strength <= 2) {
                strengthBar.className = "password-strength-bar bg-red-500";
                strengthText.textContent = "Débil";
                strengthText.className = "password-strength-text text-red-500";
            } else if (strength === 3 || strength === 4) {
                strengthBar.className = "password-strength-bar bg-yellow-500";
                strengthText.textContent = "Media";
                strengthText.className = "password-strength-text text-yellow-500";
            } else if (strength === 5) {
                strengthBar.className = "password-strength-bar bg-green-500";
                strengthText.textContent = "Fuerte";
                strengthText.className = "password-strength-text text-green-500";
            }
        });
    }
});