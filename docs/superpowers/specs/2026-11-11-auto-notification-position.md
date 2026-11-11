# Automatic notification position

Approved direction: the user chose **Tự động** after comparing the five HTML mockups.

Show notifications outside the rendered pet, within the pet's selected monitor work area. Prefer above, then right, left, below when the full message fits; otherwise use the best available rectangle and allow scrolling. Use a 12-DIP gap. Never resize or move the pet just to show a notification.

Keep the previously requested interaction: clicking the notification advances the FIFO queue or dismisses its final entry; a small History icon at the upper right opens session-only history without advancing. Full title/body must remain readable through a scroll viewer. Pause expiry while the pointer is over the notification so a long message can be read.

The notification must follow pet movement, size, monitor and DPI changes, and disappear on dismiss, pause, Tuck Away and exit. It must not steal focus when appearing. No new placement setting, dependency, release, or history persistence is needed.

An owned modeless WPF window provides room beyond the fixed pet window while retaining the existing XAML controls and namescope. Enlarging the pet window would change roaming/drag bounds; a Popup would require separate tracking of owner-window motion.
