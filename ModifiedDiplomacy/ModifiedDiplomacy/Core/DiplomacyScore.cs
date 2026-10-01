using System.Collections.Generic;
using System.Linq;

namespace ModifiedDiplomacy.Core
{
    /// <summary>
    /// Explainable score used by clan voting and foreign acceptance. Every
    /// contribution is retained for logs and tooltips.
    /// </summary>
    public sealed class DiplomacyScore
    {
        private readonly List<DiplomacyScoreComponent> _components =
            new List<DiplomacyScoreComponent>();

        public IReadOnlyList<DiplomacyScoreComponent> Components =>
            _components;

        public float Total => _components.Sum(component => component.Value);

        public void Add(string id, float value)
        {
            _components.Add(new DiplomacyScoreComponent(id, value));
        }
    }

    public sealed class DiplomacyScoreComponent
    {
        public DiplomacyScoreComponent(string id, float value)
        {
            Id = id ?? string.Empty;
            Value = value;
        }

        public string Id { get; }

        public float Value { get; }
    }
}
