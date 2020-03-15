namespace Ikeran.PokemonShuffler
{
    public class RandomizeRules
    {
        public TrainerRules TrainerRules = new TrainerRules();
        public WildPokemonRules WildPokemonRules = new WildPokemonRules();
        public string DefaultSeed;
        public EvolutionRandomization Evolution;
    }

    public class PaletteRandomization
    {
        public bool Randomize;
        public bool TypeColors;
        public bool RandomizeSecondaryColors = true;
    }

    public enum GymRandomization
    {
        Vanilla,
        Random,
        TypeThemed,
    }

    public class TrainerRules
    {
        public bool RandomizeRecurringCharacters;
        public bool RandomizeTrainers;
        public bool SillyClassNames;
        public bool RandomizeTrainerNames;
        public GymRandomization Gyms = GymRandomization.Vanilla;
    }

    public enum WildRandomization
    {
        Vanilla,
        Area,
        Individual,
    }

    public class WildPokemonRules
    {
        public WildRandomization Randomization = WildRandomization.Vanilla;
        public bool EnsureAllPhylaAppear = true;
    }

    public enum EvolutionRandomization
    {
        Vanilla,
        Random,
        Monophyletic,
        TypePhyletic,
    }
}
