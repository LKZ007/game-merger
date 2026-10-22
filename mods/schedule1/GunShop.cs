using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using S1API.Items;
using S1API.Shops;

namespace Frankenstein
{
    /// <summary>The merged game's guns for sale in a host-game shop (Hood Warfare's arsenal at Handy Hank's
    /// hardware store). Frankenstein writes the list ("AK47=Choppa@2500") and which shop into settings.json.
    /// Prices are the game's money. Retried every second until the shops exist in the loaded save.</summary>
    internal static class GunShop
    {
        static bool _done;
        static int _tries;

        public static void Tick()
        {
            if (_done || Plugin.Shop.Count == 0 || _tries > 600) return;
            _tries++;
            Shop[] shops;
            try { shops = ShopManager.GetAllShops(); } catch (Exception) { return; }
            if (shops == null || shops.Length == 0) return;
            var want = (Plugin.ShopName ?? "hardware").ToLowerInvariant();
            var shop = shops.FirstOrDefault(s => s != null && (s.Name ?? "").ToLowerInvariant().Contains(want))
                       ?? shops.FirstOrDefault(s => s != null && (s.Name ?? "").ToLowerInvariant().Contains("hank"));
            if (shop == null) return;
            int added = 0;
            var missing = new List<string>();
            foreach (var entry in Plugin.Shop)
            {
                var at = entry.Split('@');
                var name = at[0].Split('=')[0];
                float price;
                if (at.Length < 2 || !float.TryParse(at[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out price)) price = 500f;
                var id = Story.ItemId(name);
                if (id == null) { missing.Add(name); continue; }
                try
                {
                    if (shop.HasItem(id)) { added++; continue; }
                    var def = ItemManager.GetDefinition(id);
                    if (def == null) { missing.Add(name); continue; }
                    shop.AddItem(def, price);
                    added++;
                }
                catch (Exception e) { missing.Add(name + " (" + e.Message + ")"); }
            }
            _done = true;
            Melon<Plugin>.Logger.Msg("Gun shop: " + added + " item(s) for sale at " + shop.Name +
                                     (missing.Count > 0 ? ". Not in this game: " + string.Join(", ", missing) : ""));
        }
    }
}
