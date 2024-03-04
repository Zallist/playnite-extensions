using System;

namespace IGDBMetadataPlugin.IGDB
{
    public class IGDBAgeRating
    {
        public int id { get; set; }
        public AgeRatingCategory category { get; set; }
        public AgeRatingRating rating { get; set; }

        public override string ToString()
        {
            var c = Enum.GetName(typeof(AgeRatingCategory), category);
            var r = Enum.GetName(typeof(AgeRatingRating), rating);

            r = r.Replace($"{c}", "").Trim('_');

            return $"{c} {r}".Trim();
        }

        public enum AgeRatingCategory
        {
            ESRB = 1,
            PEGI = 2,
            CERO = 3,
            USK = 4,
            GRAC = 5,
            CLASSIND = 6,
            ACB = 7
        }

        public enum AgeRatingRating
        {
            _3 = 1,
            _7 = 2,
            _12 = 3,
            _16 = 4,
            _18 = 5,
            RP = 6,
            EC = 7,
            E = 8,
            E10 = 9,
            T = 10,
            M = 11,
            AO = 12,
            CERO_A = 13,
            CERO_B = 14,
            CERO_C = 15,
            CERO_D = 16,
            CERO_Z = 17,
            USK_0 = 18,
            USK_6 = 19,
            USK_12 = 20,
            USK_16 = 21,
            USK_18 = 22,
            GRAC_All = 23,
            GRAC_12 = 24,
            GRAC_15 = 25,
            GRAC_18 = 26,
            GRAC_TESTING = 27,
            CLASSIND_L = 28,
            CLASSIND_10 = 29,
            CLASSIND_12 = 30,
            CLASSIND_14 = 31,
            CLASSIND_16 = 32,
            CLASSIND_18 = 33,
            ACB_G = 34,
            ACB_PG = 35,
            ACB_M = 36,
            ACB_MA15 = 37,
            ACB_R18 = 38,
            ACB_RC = 39
        }
    }
}