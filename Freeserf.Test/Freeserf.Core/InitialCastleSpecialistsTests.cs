using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Freeserf.Test.Freeserf.Core
{
    [TestClass]
    public class InitialCastleSpecialistsTests
    {
        static Game CreateGame(uint supplies = 20)
        {
            var map = new Map(new MapGeometry(3), null);
            map.InitTiles(new FlatGrassMapGenerator(map));
            var game = new Game(map);
            game.CreateSerf(0);
            game.CreateBuilding(0);
            game.InsertPlayer(0, 0, supplies, 20);
            game.InitKnights();
            return game;
        }

        static Inventory CreateInventory(Game game)
        {
            var inventory = game.CreateInventory();
            inventory.Player = 0;
            var castle = game.CreateBuilding(1);
            castle.Position = game.Map.Position(30, 30);
            castle.Player = 0;
            castle.Inventory = inventory;
            castle.StartBuilding(Building.Type.Castle);
            inventory.Building = castle.Index;
            return inventory;
        }

        public static IEnumerable<object[]> SupplyLevels => Enumerable.Range(0, 41)
            .Select(value => new object[] { value });

        [TestMethod]
        [DynamicData(nameof(SupplyLevels))]
        public void InitialCastle_ShouldCreateEntireIntendedRoster(int supplies)
        {
            var game = CreateGame((uint)supplies);
            var inventory = CreateInventory(game);
            inventory.ApplySuppliesPreset((uint)supplies);
            game.GetPlayer(0).BuildingFounded(game.GetBuilding(inventory.Building));
            var expected = new Dictionary<Serf.Type, int>
            {
                [Serf.Type.TransporterInventory] = 1,
                [Serf.Type.Generic] = 5,
                [Serf.Type.Knight0] = 3,
                [Serf.Type.Toolmaker] = 1,
                [Serf.Type.Lumberjack] = 1,
                [Serf.Type.Sawmiller] = 1,
                [Serf.Type.Stonecutter] = 1,
                [Serf.Type.Digger] = 1,
                [Serf.Type.Builder] = 1,
                [Serf.Type.Fisher] = 1,
                [Serf.Type.Geologist] = 2,
                [Serf.Type.Miner] = 2
            };
            for (var type = Serf.Type.Transporter; type < Serf.Type.Dead; ++type)
            {
                int count = expected.GetValueOrDefault(type);
                Assert.AreEqual(count, game.Serfs.Count(serf => serf.Index != 0 && serf.SerfType == type), type.ToString());
                // Inventory transporters are deliberately counted as transporters.
                int counter = type == Serf.Type.Transporter ? 1 : type == Serf.Type.TransporterInventory ? 0 : count;
                Assert.AreEqual((uint)counter, game.GetPlayer(0).GetSerfCount(type), $"Counter {type}");
            }
            Assert.AreEqual(5u, inventory.FreeSerfCount);
        }

        public static IEnumerable<object[]> WorkerTypes => Enumerable.Range(0, (int)Serf.Type.Geologist + 1)
            .Select(value => new object[] { value });

        [TestMethod]
        [DynamicData(nameof(WorkerTypes))]
        public void ExplicitSpecialization_ShouldAllowTwoWorkersAndKeepBothAvailable(int value)
        {
            var type = (Serf.Type)value;
            var game = CreateGame();
            var inventory = CreateInventory(game);
            for (int resource = 0; resource < 26; ++resource)
                inventory.SetCountOf((Resource.Type)resource, 100);
            var first = inventory.SpawnSerfGeneric();
            Assert.IsTrue(inventory.SpecializeSerf(first, type));
            var afterFirst = Enumerable.Range(0, 26).Select(i => inventory.GetCountOf((Resource.Type)i)).ToArray();
            var second = inventory.SpawnSerfGeneric();
            Assert.IsTrue(inventory.SpecializeSerf(second, type));
            for (int resource = 0; resource < 26; ++resource)
                Assert.AreEqual(100u - 2u * (100u - afterFirst[resource]), inventory.GetCountOf((Resource.Type)resource));
            Assert.AreEqual(0u, inventory.FreeSerfCount);
            Assert.AreEqual(2u, game.GetPlayer(0).GetSerfCount(type == Serf.Type.TransporterInventory ? Serf.Type.Transporter : type));

            var called = inventory.CallInternal(type);
            Assert.IsNotNull(called);
            called.GoOutFromInventory(inventory.Index, 0, -3);
            var remaining = called == first ? second : first;
            remaining.Update();
            Assert.AreSame(remaining, inventory.CallInternal(type));
        }

        [TestMethod]
        [DynamicData(nameof(WorkerTypes))]
        public void OrdinaryRecruitment_ShouldStillRefuseDuplicateIdleWorkers(int value)
        {
            var type = (Serf.Type)value;
            var game = CreateGame();
            var inventory = CreateInventory(game);
            for (int resource = 0; resource < 26; ++resource)
                inventory.SetCountOf((Resource.Type)resource, 100);
            inventory.SpawnSerfGeneric();
            Assert.IsNotNull(inventory.SpecializeFreeSerf(type));
            var generic = inventory.SpawnSerfGeneric();
            var resources = Enumerable.Range(0, 26).Select(i => inventory.GetCountOf((Resource.Type)i)).ToArray();
            Assert.IsNull(inventory.SpecializeFreeSerf(type));
            Assert.AreEqual(Serf.Type.Generic, generic.SerfType);
            Assert.AreEqual(1u, inventory.FreeSerfCount);
            CollectionAssert.AreEqual(resources, Enumerable.Range(0, 26).Select(i => inventory.GetCountOf((Resource.Type)i)).ToArray());
        }

        [TestMethod]
        public void ExplicitSpecialization_ShouldStillRequireTools()
        {
            var game = CreateGame();
            var inventory = CreateInventory(game);
            inventory.SetCountOf(Resource.Type.Pick, 1);
            var first = inventory.SpawnSerfGeneric();
            Assert.IsTrue(inventory.SpecializeSerf(first, Serf.Type.Miner));
            var second = inventory.SpawnSerfGeneric();
            Assert.IsFalse(inventory.SpecializeSerf(second, Serf.Type.Miner));
            Assert.AreEqual(Serf.Type.Generic, second.SerfType);
            Assert.AreEqual(0u, inventory.GetCountOf(Resource.Type.Pick));
            Assert.AreEqual(1u, inventory.FreeSerfCount);
        }
    }
}
