namespace DitoDisco.Randoom.Tests;

[TestFixture(typeof(Rule30Random))]
[TestFixture(typeof(RandomGenerator))]
public class GeneratorTests {

    private readonly BitGenerator gen;


    public GeneratorTests(Type type) {
        gen = (BitGenerator)Activator.CreateInstance(type)!;
    }

    [Test]
    public void BitSample() {
        TestContext.WriteLine($"[{gen.GetType().Name}] Bit sample:");
        for(int i = 0; i < 24; i++) {
            for(int j = 0; j < 60 /* 60 has a lot of divisors, which should help patterns stand out. */; j++) {
                TestContext.Write(gen.NextBit() ? '1' : '0');
            }
            TestContext.WriteLine();
        }
    }

}
