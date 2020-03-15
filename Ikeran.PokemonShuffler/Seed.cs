using System;
namespace Ikeran.PokemonShuffler
{
    public class Seed
    {
        public static int From(string v)
        {
            if (string.IsNullOrEmpty(v))
            {
                return Arbitrary();
            }
            if (int.TryParse(v, out int a))
            {
                return a;
            }
            return v.GetHashCode();
        }

        public static int Arbitrary()
        {
            return new DateTime().GetHashCode();
        }
    }
}
