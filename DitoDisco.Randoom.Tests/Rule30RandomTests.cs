namespace DitoDisco.Randoom.Tests;

public class Rule30RandomTests {

    [Test]
    public void ExportImportTest() {
        Rule30Random rand = new Rule30Random();

        List<byte> nextBytes = new List<byte>();

        Rule30Random.State state = rand.ExportState();

        const int COUNT = 2048;

        for(int i = 0; i < COUNT; i++) {
            nextBytes.Add(rand.NextByte());
        }

        rand.ImportState(state);

        for(int i = 0; i < nextBytes.Count; i++) {
            Assert.That(rand.NextByte(), Is.EqualTo(nextBytes[i]));
        }
    }

    [Test]
    public void CompactExportImportTest() {
        Rule30Random rand = new Rule30Random();

        List<byte> nextBytes = new List<byte>();

        Rule30Random.CompactState state = rand.ExportCompactState();

        const int COUNT = 2048;

        for(int i = 0; i < COUNT; i++) {
            nextBytes.Add(rand.NextByte());
        }

        rand.ImportCompactState(state);

        for(int i = 0; i < nextBytes.Count; i++) {
            Assert.That(rand.NextByte(), Is.EqualTo(nextBytes[i]));
        }
    }

}
