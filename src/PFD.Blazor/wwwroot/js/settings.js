// Scroll sync for time roller
window.syncScroll = function (source, target) {
    if (source && target) {
        target.scrollTop = source.scrollTop;
    }
};

window.scrollToHour = function (timeRoller, tasksPanel, hour) {
    const slotHeight = 50; // Height of each time slot
    const scrollPosition = hour * slotHeight;

    if (timeRoller) {
        timeRoller.scrollTop = scrollPosition;
    }
    if (tasksPanel) {
        tasksPanel.scrollTop = scrollPosition;
    }
};

// Get the date of the day-slot that is centered in the horizontal day roller
window.getCenteredDate = function (dayRollerTrack) {
    if (!dayRollerTrack) return null;

    const container = dayRollerTrack;
    const containerRect = container.getBoundingClientRect();
    const centerX = containerRect.left + containerRect.width / 2;

    // Find all day-slot elements
    const slots = container.querySelectorAll('.day-slot');
    let closestSlot = null;
    let closestDistance = Infinity;

    slots.forEach(slot => {
        const slotRect = slot.getBoundingClientRect();
        const slotCenterX = slotRect.left + slotRect.width / 2;
        const distance = Math.abs(centerX - slotCenterX);

        if (distance < closestDistance) {
            closestDistance = distance;
            closestSlot = slot;
        }
    });

    if (closestSlot) {
        return closestSlot.getAttribute('data-date');
    }

    return null;
};

// Scroll the day roller to center on a specific date
window.scrollDayRollerToDate = function (dayRollerTrack, dateStr) {
    if (!dayRollerTrack) return;

    const slot = dayRollerTrack.querySelector(`[data-date="${dateStr}"]`);
    if (slot) {
        slot.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
    }
};

window.settingsInterop = {
    getDeviceId: function () {
        let deviceId = localStorage.getItem('pfd_device_id');
        if (!deviceId) {
            deviceId = 'device_' + Date.now() + '_' + Math.random().toString(36).substring(2, 11);
            localStorage.setItem('pfd_device_id', deviceId);
        }
        return deviceId;
    },

    getTheme: function () {
        return localStorage.getItem('pfd_theme') || 'teal';
    },

    setTheme: function (theme) {
        localStorage.setItem('pfd_theme', theme);
    },

    getIsDailyView: function () {
        const value = localStorage.getItem('pfd_is_daily_view');
        return value === null ? true : value === 'true';
    },

    setIsDailyView: function (isDailyView) {
        localStorage.setItem('pfd_is_daily_view', isDailyView.toString());
    },

    getUseLargeText: function () {
        const value = localStorage.getItem('pfd_use_large_text');
        return value === 'true';
    },

    setUseLargeText: function (useLargeText) {
        localStorage.setItem('pfd_use_large_text', useLargeText.toString());
    },

    getHighContrast: function () {
        const value = localStorage.getItem('pfd_high_contrast');
        return value === 'true';
    },

    setHighContrast: function (highContrast) {
        localStorage.setItem('pfd_high_contrast', highContrast.toString());
    }
};

// Global ESC key handler for deselecting/closing modals
window.registerEscHandler = function (dotNetRef) {
    // Remove any existing handler
    if (window.pfdEscHandler) {
        document.removeEventListener('keydown', window.pfdEscHandler);
    }

    window.pfdDotNetRef = dotNetRef;
    window.pfdEscHandler = function (e) {
        if (e.key === 'Escape') {
            // Don't intercept if user is typing in an input
            if (e.target.matches('input, textarea, select')) {
                return;
            }
            e.preventDefault();
            if (window.pfdDotNetRef) {
                window.pfdDotNetRef.invokeMethodAsync('HandleEscapeKeyJS');
            }
        }
    };
    document.addEventListener('keydown', window.pfdEscHandler);
};
