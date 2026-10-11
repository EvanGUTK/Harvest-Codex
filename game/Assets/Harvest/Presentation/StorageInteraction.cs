#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Harvest.Application;
namespace Harvest.Presentation
{
    // Presentation confirmation protocol only. Storage owns paths, byte validation and ID lifetime.
    internal sealed class StorageInteraction
    {
        private readonly SessionController controller;
        private readonly ISessionStorage? storage;
        private SessionMode knownMode, offerMode, pendingMode;
        private IReadOnlyList<RecoveryChoice> offers = Array.Empty<RecoveryChoice>();
        private string pendingId = "", confirmation = "", code = "none", message;
        private bool pendingLoad;
        public StorageInteraction(SessionController controller, ISessionStorage? storage)
        { this.controller = controller; this.storage = storage; knownMode = controller.Mode; message = storage == null ? "Storage unavailable in this session." : "Save stops execution. Load and recovery replace the current farm and draft after confirmation."; }
        public StorageViewState Read()
        {
            SyncMode();
            return new StorageViewState(storage != null, controller.Status == SessionStatus.Stopped, code, message, confirmation, offers);
        }
        public void Invalidate()
        { Clear(true); knownMode = controller.Mode; code = "none"; message = storage == null ? "Storage unavailable in this session." : "Pending replacement and recovery choices cleared."; }
        public void Save()
        {
            SyncMode();
            if (storage == null) return;
            Clear(true); Publish(storage.Save());
        }
        public void RequestLoad()
        {
            SyncMode();
            if (!Ready()) return;
            Clear(true); pendingLoad = true; pendingMode = controller.Mode;
            confirmation = "Replace the current " + ModeName() + " farm and draft with the saved farm?";
            code = "confirmation_required"; message = "Loading remains stopped. Confirm replacement or cancel.";
        }
        public void RequestRecovery(string id)
        {
            SyncMode();
            if (!Ready()) return;
            var choice = offerMode == controller.Mode ? offers.FirstOrDefault(c => c.Id == id) : null;
            if (choice == null) { Clear(true); Fail("invalid_recovery", "Recovery choice expired or is unavailable. Load again to inspect current recovery choices."); return; }
            pendingLoad = false; pendingId = choice.Id; pendingMode = controller.Mode;
            confirmation = "Replace the current " + ModeName() + " farm and draft using " + choice.Label + "?";
            code = "confirmation_required"; message = "Recovery revalidates the selected save before replacement. Confirm or cancel.";
        }
        public void Confirm()
        {
            SyncMode();
            if (!Ready()) { Clear(true); return; }
            if (confirmation.Length == 0 || pendingMode != controller.Mode)
            { Clear(true); Fail("invalid_confirmation", "Replacement confirmation expired. Request loading or recovery again."); return; }
            bool load = pendingLoad; string id = pendingId;
            if (!load && (offerMode != controller.Mode || !offers.Any(c => c.Id == id)))
            { Clear(true); Fail("invalid_recovery", "Recovery choice expired. Load again to inspect recovery choices."); return; }
            // Keep service offers alive until Recover consumes the ID; only clear local state here.
            Clear(false);
            Publish(load ? storage!.Load() : storage!.Recover(id));
        }
        public void Cancel()
        {
            SyncMode(); pendingLoad = false; pendingId = ""; confirmation = "";
            code = "cancelled"; message = "Replacement cancelled. Current farm and draft retained.";
        }
        private bool Ready()
        {
            if (storage == null) return false;
            if (controller.Status == SessionStatus.Stopped) return true;
            Fail("session_active", "Stop execution before loading or recovering a farm. Current farm and draft retained."); return false;
        }
        private void SyncMode()
        { if (knownMode != controller.Mode) { Invalidate(); } }
        private string ModeName() => controller.Mode == SessionMode.Campaign ? "campaign" : "sandbox";
        private void Clear(bool invalidateService)
        {
            offers = Array.Empty<RecoveryChoice>(); confirmation = ""; pendingId = ""; pendingLoad = false;
            if (invalidateService) storage?.InvalidateRecovery();
        }
        private void Publish(StorageOutcome outcome)
        { code = outcome.Code; message = outcome.Message; offers = outcome.RecoveryChoices; offerMode = controller.Mode; }
        private void Fail(string failureCode, string text) { code = failureCode; message = text; }
    }
}
