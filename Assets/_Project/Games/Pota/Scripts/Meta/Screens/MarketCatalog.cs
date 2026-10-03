using UnityEngine;

namespace Project.Meta
{
    /// <summary>The showcase's slice of Pota's katalog.ts: jerseys and balls with their prices and colours (price multiplier 1).</summary>
    public static class MarketCatalog
    {
        public enum Category { Clothes, Balls, Boosters, Coins }

        public class Item
        {
            public string Id, Name;
            public Category Category;
            public int Price;
            public Color Colour;
        }

        private static Color Hex(uint rgb) => ColorHunt.PotaLook.Hex(rgb);

        public static readonly Item[] Jerseys =
        {
            new Item { Id = "forma-mavi", Name = "Mavi forma", Category = Category.Clothes, Price = 0, Colour = Hex(0x2b55c7) },
            new Item { Id = "forma-kirmizi", Name = "Kırmızı forma", Category = Category.Clothes, Price = 120, Colour = Hex(0xd63b2f) },
            new Item { Id = "forma-yesil", Name = "Yeşil forma", Category = Category.Clothes, Price = 120, Colour = Hex(0x2e9e5b) },
            new Item { Id = "forma-mor", Name = "Mor forma", Category = Category.Clothes, Price = 140, Colour = Hex(0x7a3fd1) },
            new Item { Id = "forma-siyah", Name = "Siyah forma", Category = Category.Clothes, Price = 160, Colour = Hex(0x1b1b1f) },
        };

        public static readonly Item[] Balls =
        {
            new Item { Id = "top-klasik", Name = "Klasik top", Category = Category.Balls, Price = 0, Colour = Hex(0xf07d1d) },
            new Item { Id = "top-sokak", Name = "Sokak topu", Category = Category.Balls, Price = 200, Colour = Hex(0x2a2a2e) },
            new Item { Id = "top-neon", Name = "Neon top", Category = Category.Balls, Price = 250, Colour = Hex(0x9dff3a) },
            new Item { Id = "top-retro", Name = "Retro top", Category = Category.Balls, Price = 300, Colour = Hex(0xb5703c) },
            new Item { Id = "top-altin", Name = "Altın top", Category = Category.Balls, Price = 500, Colour = Hex(0xf3c340) },
        };

        public static readonly (string name, string note, int price)[] Boosters =
        {
            ("Altın top", "Bu denemede +1 altın top", 40), ("+%15 cephane", "Bu denemede %15 daha çok top", 40), ("Devam", "Kaybedince +16 top", 60),
        };

        public static readonly (string name, string note, string price)[] CoinPacks =
        {
            ("550 para", "5 kez Devam", "₺29,99"), ("1200 para", "12 kez Devam", "₺54,99"), ("5 can", "Canlar tam dolar", "₺19,99"),
        };
    }
}
