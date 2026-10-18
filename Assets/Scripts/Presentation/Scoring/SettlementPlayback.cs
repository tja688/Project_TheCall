using System;
using QFramework;
using UnityEngine;

namespace TheCall.Scoring
{
    // Owns the rule handoff. ScoringShow consumes a tape and reports only visual progress.
    internal sealed class SettlementPlayback
    {
        readonly IArchitecture _architecture;
        ScoringShow _show;
        bool _playing;
        Action _completed;

        public SettlementPlayback(IArchitecture architecture) => _architecture = architecture;
        public bool IsPlaying => _playing;

        public bool TryStart(OperationScreenView screen, Action<string> say, Action completed)
        {
            if (_playing || ScoringShow.IsPlaying || screen == null || !screen.gameObject.activeInHierarchy
                || _architecture.SendQuery(new RunPhaseQuery()) != RunPhase.Operation) return false;
            var initialEnergy = _architecture.SendQuery(new LevelEnergyQuery());
            var initialGold = _architecture.SendQuery(new RunLedgerQuery()).Gold;
            var cells = _architecture.SendQuery(new ExtractionSlotsQuery());
            var ids = new string[screen.extractionSlots?.Length ?? 0];
            for (var i = 0; i < ids.Length && i < cells.Count; i++) ids[i] = cells[i].MonsterId;
            _playing = true;
            _completed = completed;
            try
            {
                _architecture.SendCommand(new ConfirmSettlementCommand());
                var tape = ScoringTape.Arrange(_architecture.SendQuery(new SettlementRecordQuery()), ids, initialEnergy);
                _architecture.SendCommand(new ShowSettlementSightCommand());
                _show = ScoringShow.Play(new ScoringStage(tape, screen.extractionSlots,
                    screen.transform as RectTransform, screen.dragLayer, screen.goldLabel, screen.currentNumber,
                    say, Finish, initialEnergy, initialGold,
                    (actor, target) => _architecture.SendCommand(new MoveSettlementSightCommand(actor, target)),
                    cell => _architecture.SendCommand(new ClearSettlementSightCommand(cell)),
                    energy => { if (screen.lab != null && screen.lab.facility == null) screen.lab.EnergyShown(energy); },
                    screen.lab != null ? screen.lab.facility : null));
                return true;
            }
            catch
            {
                Cancel();
                throw;
            }
        }

        public void Cancel()
        {
            // Cancellation retires callbacks before an owner resets or destroys its Architecture.
            _completed = null;
            if (_show != null) _show.Cancel();
            Finish();
        }

        void Finish()
        {
            if (!_playing) return;
            _playing = false;
            _show = null;
            _architecture.SendCommand(new HideSettlementSightCommand());
            var completed = _completed;
            _completed = null;
            completed?.Invoke();
        }
    }
}
