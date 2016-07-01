using System;

namespace ArachneControlerDotNet
{
    public class SummaryModel
    {
        public int Passed { get; set; }

        public int Failed { get; set; }

        public int Skipped { get; set; }

        public int Total { get; set; }

        public double Percentage { get; set; }

        public double CountPercent(int pass, int skip, int fail)
        {
            var total = Convert.ToDouble(pass) + Convert.ToDouble(fail);
            if (total > 0)
            {
                var result = pass / total * 100.00;
                return result;
            }
            return 0;
        }
    }
}

