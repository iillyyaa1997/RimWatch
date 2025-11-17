using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Represents a scored location for building placement.
    /// Provides detailed breakdown of why a location is good or bad.
    /// </summary>
    public class PlacementScore
    {
        /// <summary>
        /// Total score from 0-100.
        /// </summary>
        public int TotalScore { get; private set; }

        /// <summary>
        /// Individual scoring factors.
        /// </summary>
        public Dictionary<string, int> Factors { get; private set; }

        /// <summary>
        /// Whether this location passed all critical checks.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Reasons why location was rejected (if IsValid = false).
        /// </summary>
        public List<string> RejectionReasons { get; private set; }

        public PlacementScore()
        {
            TotalScore = 0;
            Factors = new Dictionary<string, int>();
            IsValid = true;
            RejectionReasons = new List<string>();
        }

        /// <summary>
        /// Add a scoring factor (positive or negative).
        /// </summary>
        public void AddFactor(string name, int score)
        {
            Factors[name] = score;
            RecalculateTotal();
        }

        /// <summary>
        /// Mark location as invalid with a reason.
        /// </summary>
        public void Reject(string reason)
        {
            IsValid = false;
            RejectionReasons.Add(reason);
        }

        /// <summary>
        /// Recalculates total score from all factors.
        /// </summary>
        private void RecalculateTotal()
        {
            int sum = 0;
            foreach (int value in Factors.Values)
            {
                sum += value;
            }
            
            // Clamp to 0-100
            TotalScore = System.Math.Max(0, System.Math.Min(100, sum));
        }

        /// <summary>
        /// Get a human-readable breakdown of the score.
        /// </summary>
        public string GetBreakdown()
        {
            StringBuilder sb = new StringBuilder();
            
            if (!IsValid)
            {
                sb.AppendLine($"❌ Invalid Location (Rejected)");
                foreach (string reason in RejectionReasons)
                {
                    sb.AppendLine($"   - {reason}");
                }
                return sb.ToString();
            }

            sb.AppendLine($"Score: {TotalScore}/100");
            
            foreach (var factor in Factors)
            {
                string symbol = factor.Value >= 0 ? "✓" : "✗";
                sb.AppendLine($"   {symbol} {factor.Key}: {factor.Value:+#;-#;0}");
            }

            return sb.ToString();
        }
        
        /// <summary>
        /// Get top N scoring factors (for logging).
        /// </summary>
        public List<string> GetTopFactors(int count)
        {
            return Factors
                .OrderByDescending(f => System.Math.Abs(f.Value))
                .Take(count)
                .Select(f => f.Key)
                .ToList();
        }

        /// <summary>
        /// Get a short summary string.
        /// </summary>
        public override string ToString()
        {
            if (!IsValid)
            {
                return $"Invalid ({string.Join(", ", RejectionReasons)})";
            }
            return $"{TotalScore}/100";
        }
    }
}

