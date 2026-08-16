// Development token helper for Swagger UI
(function() {
    'use strict';

    // Wait for Swagger UI to load
    function waitForSwaggerUI() {
        if (typeof window.ui !== 'undefined') {
            initializeDevTokenHelper();
        } else {
            setTimeout(waitForSwaggerUI, 100);
        }
    }

    function initializeDevTokenHelper() {
        // Add development token helper to the page
        addDevTokenHelper();
        
        // Auto-fill token from URL parameters if available
        const urlParams = new URLSearchParams(window.location.search);
        const devToken = urlParams.get('dev_token');
        if (devToken) {
            setTimeout(() => {
                fillTokenAutomatically('Bearer ' + devToken);
            }, 1000);
        }
    }

    function addDevTokenHelper() {
        const infoSection = document.querySelector('.swagger-ui .info');
        if (!infoSection || document.querySelector('.dev-token-helper')) {
            return; // Already added or info section not found
        }

        const helperDiv = document.createElement('div');
        helperDiv.className = 'dev-token-helper';
        helperDiv.innerHTML = `
            <div class="token-label">🔧 Development Tools</div>
            <div style="margin-bottom: 10px;">
                <button onclick="generateDevToken()">Generate Dev Token</button>
                <button onclick="generatePermanentToken()">Generate Permanent Token</button>
                <button onclick="clearToken()">Clear Token</button>
            </div>
            <div id="token-display" style="display: none;">
                <div class="token-label">Generated Token:</div>
                <div class="token-value" id="token-value"></div>
                <div class="token-actions">
                    <button onclick="copyToken()">Copy Token</button>
                    <button onclick="useToken()">Use Token</button>
                </div>
            </div>
            <div style="margin-top: 10px; font-size: 0.875rem; color: #6b7280;">
                💡 <strong>Quick Start:</strong> Click "Generate Dev Token" → "Use Token" → Try any protected endpoint!
            </div>
        `;

        infoSection.appendChild(helperDiv);
    }

    // Make functions globally available
    window.generateDevToken = async function() {
        try {
            const response = await fetch('/api/dev/token');
            if (response.ok) {
                const data = await response.json();
                displayToken(data.token, 'Development Token (24h expiry)');
            } else {
                alert('Failed to generate token. Make sure you are in development environment.');
            }
        } catch (error) {
            console.error('Error generating dev token:', error);
            alert('Error generating token. Check console for details.');
        }
    };

    window.generatePermanentToken = async function() {
        try {
            const response = await fetch('/api/dev/permanent-token');
            if (response.ok) {
                const data = await response.json();
                displayToken(data.token, 'Permanent Test Token (1 year expiry)');
            } else {
                alert('Failed to generate permanent token. Make sure you are in development environment.');
            }
        } catch (error) {
            console.error('Error generating permanent token:', error);
            alert('Error generating token. Check console for details.');
        }
    };

    window.copyToken = function() {
        const tokenValue = document.getElementById('token-value').textContent;
        const fullToken = 'Bearer ' + tokenValue;
        
        if (navigator.clipboard) {
            navigator.clipboard.writeText(fullToken).then(() => {
                showNotification('Token copied to clipboard!', 'success');
            });
        } else {
            // Fallback for older browsers
            const textArea = document.createElement('textarea');
            textArea.value = fullToken;
            document.body.appendChild(textArea);
            textArea.select();
            document.execCommand('copy');
            document.body.removeChild(textArea);
            showNotification('Token copied to clipboard!', 'success');
        }
    };

    window.useToken = function() {
        const tokenValue = document.getElementById('token-value').textContent;
        fillTokenAutomatically('Bearer ' + tokenValue);
    };

    window.clearToken = function() {
        fillTokenAutomatically('');
        document.getElementById('token-display').style.display = 'none';
        showNotification('Token cleared', 'info');
    };

    function displayToken(token, label) {
        const tokenDisplay = document.getElementById('token-display');
        const tokenValueElement = document.getElementById('token-value');
        
        tokenValueElement.textContent = token;
        tokenDisplay.style.display = 'block';
        
        // Update label
        const labelElement = tokenDisplay.querySelector('.token-label');
        labelElement.textContent = label + ':';
        
        showNotification('Token generated successfully!', 'success');
    }

    function fillTokenAutomatically(tokenValue) {
        try {
            // Find the authorize button and click it
            const authorizeBtn = document.querySelector('.btn.authorize');
            if (authorizeBtn) {
                authorizeBtn.click();
                
                // Wait for the modal to appear and fill the token
                setTimeout(() => {
                    const tokenInput = document.querySelector('input[placeholder*="Bearer"], input[name="Bearer"]');
                    if (tokenInput) {
                        tokenInput.value = tokenValue;
                        
                        // Trigger the authorize button in the modal
                        const modalAuthorizeBtn = document.querySelector('.auth-btn-wrapper .btn-done, .auth-container .authorize');
                        if (modalAuthorizeBtn) {
                            modalAuthorizeBtn.click();
                            showNotification('Token applied successfully!', 'success');
                        }
                    }
                }, 500);
            }
        } catch (error) {
            console.error('Error filling token:', error);
            showNotification('Error applying token. Please copy and paste manually.', 'error');
        }
    }

    function showNotification(message, type) {
        const notification = document.createElement('div');
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            padding: 12px 24px;
            border-radius: 6px;
            color: white;
            font-weight: 500;
            z-index: 10000;
            animation: slideIn 0.3s ease-out;
        `;
        
        const colors = {
            success: '#10b981',
            error: '#ef4444', 
            info: '#3b82f6'
        };
        
        notification.style.backgroundColor = colors[type] || colors.info;
        notification.textContent = message;
        
        document.body.appendChild(notification);
        
        setTimeout(() => {
            notification.style.animation = 'slideOut 0.3s ease-in';
            setTimeout(() => {
                document.body.removeChild(notification);
            }, 300);
        }, 3000);
    }

    // Add CSS animations
    const style = document.createElement('style');
    style.textContent = `
        @keyframes slideIn {
            from { transform: translateX(100%); opacity: 0; }
            to { transform: translateX(0); opacity: 1; }
        }
        @keyframes slideOut {
            from { transform: translateX(0); opacity: 1; }
            to { transform: translateX(100%); opacity: 0; }
        }
    `;
    document.head.appendChild(style);

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', waitForSwaggerUI);
    } else {
        waitForSwaggerUI();
    }
})();
