using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    /// <summary>
    /// 结算演出正在走的那一排。结算写完时模型已经是终局，演出按开场格子换位、消失，悬停读这一排。
    /// </summary>
    internal sealed class SettlementSight : AbstractModel
    {
        readonly Dictionary<string, Monster> _cast = new Dictionary<string, Monster>();
        string[] _cells;

        public bool Showing { get; private set; }

        public IReadOnlyList<string> Cells => _cells;

        public void Capture(LevelModel level, RunModel run)
        {
            var cells = level.CaptureExtraction();
            _cells = cells;
            _cast.Clear();
            for (var i = 0; i < cells.Length; i++)
            {
                var id = cells[i];
                if (string.IsNullOrEmpty(id) || _cast.ContainsKey(id))
                    continue;

                var monster = run.Find(id);
                if (monster != null)
                    _cast[id] = monster;
            }

            Showing = false;
        }

        public void Show() => Showing = _cells != null;

        public void Hide()
        {
            Showing = false;
            _cells = null;
            _cast.Clear();
        }

        public void Swap(int actor, int target)
        {
            if (!Showing || _cells == null)
                return;
            if (actor < 0 || target < 0 || actor >= _cells.Length || target >= _cells.Length || actor == target)
                return;

            var id = _cells[actor];
            _cells[actor] = _cells[target];
            _cells[target] = id;
        }

        public void Clear(int cell)
        {
            if (!Showing || _cells == null || cell < 0 || cell >= _cells.Length)
                return;

            _cells[cell] = null;
        }

        public bool Contains(string monsterId)
        {
            if (!Showing || _cells == null || string.IsNullOrEmpty(monsterId))
                return false;

            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == monsterId)
                    return true;
            }

            return false;
        }

        public Monster Find(string monsterId)
        {
            if (monsterId != null && _cast.TryGetValue(monsterId, out var monster))
                return monster;

            return null;
        }

        protected override void OnInit()
        {
        }
    }

    public sealed class ShowSettlementSightCommand : AbstractCommand
    {
        protected override void OnExecute() => this.GetModel<SettlementSight>().Show();
    }

    public sealed class HideSettlementSightCommand : AbstractCommand
    {
        protected override void OnExecute() => this.GetModel<SettlementSight>().Hide();
    }

    public sealed class MoveSettlementSightCommand : AbstractCommand
    {
        readonly int _actor;
        readonly int _target;

        public MoveSettlementSightCommand(int actor, int target)
        {
            _actor = actor;
            _target = target;
        }

        protected override void OnExecute() => this.GetModel<SettlementSight>().Swap(_actor, _target);
    }

    public sealed class ClearSettlementSightCommand : AbstractCommand
    {
        readonly int _cell;

        public ClearSettlementSightCommand(int cell) => _cell = cell;

        protected override void OnExecute() => this.GetModel<SettlementSight>().Clear(_cell);
    }
}
