using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Freeserf.Test.Freeserf.Core
{
    [TestClass]
    public class InventorySerfOutTests
    {
        static (Game game, Inventory inventory, Serf[] workers) CreateStock(Serf.Type type, int count)
        {
            var map = new Map(new MapGeometry(3), null);
            map.InitTiles(new FlatGrassMapGenerator(map));
            var game = new Game(map);
            game.CreateSerf(0);
            game.InsertPlayer(0, 0, 20, 20);
            var inventory = game.CreateInventory();
            inventory.Player = 0;
            var stock = game.CreateBuilding(1);
            stock.Position = game.Map.Position(10, 10);
            stock.Player = 0;
            stock.Inventory = inventory;
            inventory.Building = stock.Index;
            var workers = Enumerable.Range(0, count).Select(_ => inventory.SpawnSerfGeneric()).ToArray();
            if (type != Serf.Type.Generic)
            {
                for (int resource = 0; resource < 26; ++resource)
                    inventory.SetCountOf((Resource.Type)resource, 100);
                foreach (var worker in workers)
                    Assert.IsTrue(inventory.SpecializeSerf(worker, type));
            }
            // Keep everyone physically inside while inspecting the departure queue.
            var blocker = game.CreateSerf(100);
            blocker.Position = stock.Position;
            game.Map.SetSerfIndex(stock.Position, (int)blocker.Index);
            return (game, inventory, workers);
        }

        [TestMethod]
        [DataRow((int)Serf.Type.Generic, (int)Inventory.Mode.In)]
        [DataRow((int)Serf.Type.Generic, (int)Inventory.Mode.Stop)]
        [DataRow((int)Serf.Type.Miner, (int)Inventory.Mode.In)]
        [DataRow((int)Serf.Type.Miner, (int)Inventory.Mode.Stop)]
        [DataRow((int)Serf.Type.Geologist, (int)Inventory.Mode.In)]
        [DataRow((int)Serf.Type.Geologist, (int)Inventory.Mode.Stop)]
        [DataRow((int)Serf.Type.Builder, (int)Inventory.Mode.In)]
        [DataRow((int)Serf.Type.Builder, (int)Inventory.Mode.Stop)]
        public void OutMode_ShouldQueueOnlyThreeAndSwitchingBackShouldKeepTheRestInside(int typeValue, int modeValue)
        {
            var type = (Serf.Type)typeValue;
            var (game, inventory, workers) = CreateStock(type, 30);
            inventory.SerfMode = Inventory.Mode.Out;
            foreach (var worker in workers)
                worker.Update();

            Assert.AreEqual(3, workers.Count(s => s.SerfState == Serf.State.ReadyToLeaveInventory));
            Assert.AreEqual(27, game.GetSerfsInInventory(inventory).Count());
            Assert.AreEqual(3u, inventory.GetSerfQueueLength());
            Assert.AreEqual(type == Serf.Type.Generic ? 27u : 0u, inventory.FreeSerfCount);
            Assert.AreEqual(30u, game.GetPlayer(0).GetSerfCount(type), "Queuing must not change the player's population.");

            inventory.SerfMode = (Inventory.Mode)modeValue;
            for (int update = 0; update < 10; ++update)
                foreach (var worker in workers)
                    worker.Update();

            Assert.AreEqual(3, workers.Count(s => s.SerfState == Serf.State.ReadyToLeaveInventory),
                "Existing orders remain queued, but changing mode must stop further departures.");
            Assert.AreEqual(27, game.GetSerfsInInventory(inventory).Count());
            Assert.AreEqual(3u, inventory.GetSerfQueueLength());
            Assert.AreEqual(type == Serf.Type.Generic ? 27u : 0u, inventory.FreeSerfCount);
        }

        [TestMethod]
        public void ContinuousOutMode_WithBlockedExit_ShouldNeverExceedQueueLimit()
        {
            var (game, inventory, workers) = CreateStock(Serf.Type.Generic, 30);
            inventory.SerfMode = Inventory.Mode.Out;
            for (int update = 0; update < 20; ++update)
                foreach (var worker in workers)
                    worker.Update();

            Assert.AreEqual(3u, inventory.GetSerfQueueLength());
            Assert.AreEqual(3, workers.Count(s => s.SerfState == Serf.State.ReadyToLeaveInventory));
            Assert.AreEqual(27, game.GetSerfsInInventory(inventory).Count());
            Assert.AreEqual(27u, inventory.FreeSerfCount);
        }
    }
}
