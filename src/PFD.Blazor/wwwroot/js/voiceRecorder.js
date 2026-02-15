/**
 * Voice Recorder for PFD - Browser-based audio recording using MediaRecorder API
 */
window.voiceRecorder = {
    mediaRecorder: null,
    audioChunks: [],
    stream: null,
    dotNetRef: null,
    startTime: null,
    timerInterval: null,

    /**
     * Check if browser supports audio recording
     */
    isSupported: function() {
        return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia && window.MediaRecorder);
    },

    /**
     * Check microphone permission state
     */
    checkPermission: async function() {
        try {
            if (navigator.permissions && navigator.permissions.query) {
                const result = await navigator.permissions.query({ name: 'microphone' });
                return result.state; // 'granted', 'denied', 'prompt'
            }
            return 'unknown';
        } catch {
            return 'unknown';
        }
    },

    /**
     * Start recording audio
     * @param {object} dotNetRef - Reference to Blazor component for callbacks
     */
    startRecording: async function(dotNetRef) {
        try {
            this.dotNetRef = dotNetRef;
            this.audioChunks = [];

            // Request microphone access
            this.stream = await navigator.mediaDevices.getUserMedia({
                audio: {
                    channelCount: 1,
                    sampleRate: 16000,
                    echoCancellation: true,
                    noiseSuppression: true
                }
            });

            // Determine supported MIME type
            let mimeType = 'audio/webm';
            if (MediaRecorder.isTypeSupported('audio/webm;codecs=opus')) {
                mimeType = 'audio/webm;codecs=opus';
            } else if (MediaRecorder.isTypeSupported('audio/webm')) {
                mimeType = 'audio/webm';
            } else if (MediaRecorder.isTypeSupported('audio/ogg;codecs=opus')) {
                mimeType = 'audio/ogg;codecs=opus';
            } else if (MediaRecorder.isTypeSupported('audio/mp4')) {
                mimeType = 'audio/mp4';
            }

            this.mediaRecorder = new MediaRecorder(this.stream, { mimeType: mimeType });

            this.mediaRecorder.ondataavailable = (event) => {
                if (event.data.size > 0) {
                    this.audioChunks.push(event.data);
                }
            };

            this.mediaRecorder.onerror = async (event) => {
                console.error('MediaRecorder error:', event.error);
                if (this.dotNetRef) {
                    await this.dotNetRef.invokeMethodAsync('OnRecordingError', event.error?.message || 'Recording error');
                }
                this.cleanup();
            };

            this.mediaRecorder.onstop = async () => {
                // Audio processing will be handled by stopRecording
            };

            // Start recording with 100ms chunks
            this.mediaRecorder.start(100);
            this.startTime = Date.now();

            // Start timer updates
            this.timerInterval = setInterval(async () => {
                if (this.dotNetRef && this.startTime) {
                    const elapsed = Math.floor((Date.now() - this.startTime) / 1000);
                    await this.dotNetRef.invokeMethodAsync('OnTimerUpdate', elapsed);
                }
            }, 1000);

            return true;
        } catch (error) {
            console.error('Failed to start recording:', error);

            let errorMessage = 'Failed to start recording';
            if (error.name === 'NotAllowedError') {
                errorMessage = 'Microphone permission denied. Please allow microphone access.';
            } else if (error.name === 'NotFoundError') {
                errorMessage = 'No microphone found. Please connect a microphone.';
            } else if (error.name === 'NotSupportedError') {
                errorMessage = 'Audio recording not supported in this browser.';
            }

            throw new Error(errorMessage);
        }
    },

    /**
     * Stop recording and return audio as base64
     */
    stopRecording: async function() {
        return new Promise((resolve, reject) => {
            if (!this.mediaRecorder || this.mediaRecorder.state === 'inactive') {
                this.cleanup();
                reject(new Error('No recording in progress'));
                return;
            }

            this.mediaRecorder.onstop = async () => {
                try {
                    // Check minimum recording length
                    const duration = this.startTime ? (Date.now() - this.startTime) / 1000 : 0;
                    if (duration < 0.5) {
                        this.cleanup();
                        reject(new Error('Recording too short. Please speak for at least 1 second.'));
                        return;
                    }

                    // Combine chunks into blob
                    const mimeType = this.mediaRecorder.mimeType || 'audio/webm';
                    const blob = new Blob(this.audioChunks, { type: mimeType });

                    // Convert to base64
                    const base64 = await this.blobToBase64(blob);

                    this.cleanup();
                    resolve({
                        audioBase64: base64,
                        mimeType: mimeType,
                        durationSeconds: duration
                    });
                } catch (error) {
                    this.cleanup();
                    reject(error);
                }
            };

            this.mediaRecorder.stop();
        });
    },

    /**
     * Cancel recording without saving
     */
    cancelRecording: function() {
        if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
            this.mediaRecorder.stop();
        }
        this.cleanup();
    },

    /**
     * Convert blob to base64 string
     */
    blobToBase64: function(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onloadend = () => {
                // Remove data URL prefix (e.g., "data:audio/webm;base64,")
                const base64 = reader.result.split(',')[1];
                resolve(base64);
            };
            reader.onerror = () => reject(new Error('Failed to convert audio'));
            reader.readAsDataURL(blob);
        });
    },

    /**
     * Clean up resources
     */
    cleanup: function() {
        if (this.timerInterval) {
            clearInterval(this.timerInterval);
            this.timerInterval = null;
        }

        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
            this.stream = null;
        }

        this.mediaRecorder = null;
        this.audioChunks = [];
        this.startTime = null;
        this.dotNetRef = null;
    },

    /**
     * Get current recording duration in seconds
     */
    getDuration: function() {
        if (!this.startTime) return 0;
        return Math.floor((Date.now() - this.startTime) / 1000);
    }
};
