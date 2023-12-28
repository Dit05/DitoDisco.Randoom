using System;


namespace DitoDisco.Randoom {

    /// <summary>
    /// Uses a <see cref="System.Random"/> to generate reasonably random bits.
    /// </summary>
    public class RandomGenerator : BitGenerator {

        private readonly Random _random;
        public Random Random => _random;


        public RandomGenerator(Random random) {
            _random = random;
        }


        public override bool NextBit() => _random.NextDouble() < 0.5;

    }

}
