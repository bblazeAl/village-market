using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    public enum Lang { EN = 0, RU = 1 }

    /// <summary>Two-language string table. Keys are stable ids; missing keys fall back to English, then to the key.</summary>
    public static class Loc
    {
        public static Lang Current { get; private set; } = Lang.EN;
        public static event Action Changed;

        public static void Set(Lang lang)
        {
            if (Current == lang) return;
            Current = lang;
            Changed?.Invoke();
        }

        public static Lang SystemDefault()
            => Application.systemLanguage == SystemLanguage.Russian || Application.systemLanguage == SystemLanguage.Ukrainian
               || Application.systemLanguage == SystemLanguage.Belarusian ? Lang.RU : Lang.EN;

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (Table.TryGetValue(key, out var row))
            {
                var s = row[(int)Current];
                return string.IsNullOrEmpty(s) ? row[0] : s;
            }
            return key;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // ---- title / loading ----
            { "game.title", new[] { "Village Market", "Деревенский рынок" } },
            { "game.subtitle", new[] { "demo", "демо" } },
            { "loading", new[] { "Loading...", "Загрузка..." } },
            { "tip.1", new[] { "Stand on a price circle to build", "Встань на круг с ценой, чтобы построить" } },
            { "tip.2", new[] { "Helpers keep working while you rest", "Помощники работают, пока тебя нет" } },
            { "tip.3", new[] { "Upgrade your bag to carry more", "Улучши сумку, чтобы носить больше" } },

            // ---- items ----
            { "item.apple", new[] { "Apple", "Яблоко" } },
            { "item.bread", new[] { "Bread", "Хлеб" } },
            { "item.pie", new[] { "Apple pie", "Яблочный пирог" } },
            { "item.plate", new[] { "Dirty plate", "Грязная тарелка" } },

            // ---- pads / buildings ----
            { "pad.counter", new[] { "Checkout", "Касса" } },
            { "pad.apple_stand", new[] { "Apple stand", "Прилавок яблок" } },
            { "pad.tree", new[] { "Apple tree", "Яблоня" } },
            { "pad.oven", new[] { "Bakery oven", "Хлебная печь" } },
            { "pad.bread_shelf", new[] { "Bread shelf", "Полка для хлеба" } },
            { "pad.hire_cashier", new[] { "Hire cashier", "Нанять кассира" } },
            { "pad.upgrades", new[] { "Upgrades", "Улучшения" } },
            { "pad.helpers", new[] { "Helpers", "Помощники" } },
            { "pad.hire_picker", new[] { "Hire apple picker", "Нанять сборщика" } },
            { "pad.hire_baker", new[] { "Hire baker", "Нанять пекаря" } },
            { "pad.terrace", new[] { "Open the terrace", "Открыть террасу" } },
            { "pad.pie_oven", new[] { "Pie oven", "Печь для пирогов" } },
            { "pad.pie_shelf", new[] { "Pie shelf", "Полка пирогов" } },
            { "pad.table", new[] { "Picnic table", "Столик" } },
            { "pad.wash", new[] { "Wash tub", "Мойка" } },
            { "pad.hire_waiter", new[] { "Hire pie helper", "Нанять помощника" } },
            { "pad.oven2", new[] { "Second oven", "Вторая печь" } },

            // ---- objectives ----
            { "obj.collect_coins", new[] { "Pick up the coins", "Собери монеты" } },
            { "obj.build", new[] { "Build: {0}", "Построй: {0}" } },
            { "obj.pick_apples", new[] { "Pick apples from the tree", "Сорви яблоки с дерева" } },
            { "obj.stock_stand", new[] { "Put apples on the stand", "Выложи яблоки на прилавок" } },
            { "obj.serve", new[] { "Go to the checkout and serve", "Встань за кассу и обслужи" } },
            { "obj.collect_money", new[] { "Collect your money", "Забери деньги" } },
            { "obj.stock", new[] { "Bring {0} to the shelf", "Отнеси на полку: {0}" } },
            { "obj.take_bread", new[] { "Take bread from the oven", "Забери хлеб из печи" } },
            { "obj.feed_oven", new[] { "Bring apples to the pie oven", "Принеси яблоки в печь для пирогов" } },
            { "obj.take_pies", new[] { "Take pies from the oven", "Забери пироги из печи" } },
            { "obj.clean", new[] { "Clear the dirty plates", "Убери грязные тарелки" } },
            { "obj.wash", new[] { "Take plates to the wash tub", "Отнеси тарелки в мойку" } },
            { "obj.save_up", new[] { "Save up for: {0}", "Копи на: {0}" } },
            { "obj.upgrade", new[] { "Visit the upgrade board", "Загляни к доске улучшений" } },
            { "obj.trash", new[] { "Hands full? Drop extras in the compost bin", "Руки заняты? Выброси лишнее в компост" } },
            { "obj.all_done", new[] { "Market complete! Keep playing", "Рынок построен! Играй дальше" } },

            // ---- hud ----
            { "hud.level", new[] { "Market Lv {0}", "Рынок, ур. {0}" } },
            { "hud.max", new[] { "MAX", "МАКС" } },
            { "hud.vip", new[] { "VIP!", "VIP!" } },
            { "hud.full", new[] { "FULL", "ПОЛНО" } },
            { "hud.no_money", new[] { "Not enough coins", "Не хватает монет" } },

            // ---- upgrades ----
            { "upg.title", new[] { "Upgrades", "Улучшения" } },
            { "upg.speed", new[] { "Walk speed", "Скорость шага" } },
            { "upg.capacity", new[] { "Carry capacity", "Вместимость рук" } },
            { "upg.profit", new[] { "Prices", "Цены" } },
            { "upg.speed.d", new[] { "Move faster around the market", "Быстрее ходишь по рынку" } },
            { "upg.capacity.d", new[] { "Carry more items at once", "Носишь больше за раз" } },
            { "upg.profit.d", new[] { "Customers pay more", "Покупатели платят больше" } },
            { "helpers.title", new[] { "Helpers", "Помощники" } },
            { "upg.h_speed", new[] { "Helper speed", "Скорость помощников" } },
            { "upg.h_capacity", new[] { "Helper capacity", "Вместимость помощников" } },
            { "upg.h_cashier", new[] { "Cashier speed", "Скорость кассира" } },
            { "upg.h_speed.d", new[] { "Helpers walk faster", "Помощники ходят быстрее" } },
            { "upg.h_capacity.d", new[] { "Helpers carry more", "Помощники носят больше" } },
            { "upg.h_cashier.d", new[] { "Checkout goes quicker", "Касса работает быстрее" } },
            { "upg.maxed", new[] { "MAX", "МАКС" } },
            { "upg.level", new[] { "Lv {0}", "Ур. {0}" } },

            // ---- settings ----
            { "set.title", new[] { "Settings", "Настройки" } },
            { "set.music", new[] { "Music", "Музыка" } },
            { "set.sound", new[] { "Sound", "Звуки" } },
            { "set.lang", new[] { "Language", "Язык" } },
            { "set.lang.value", new[] { "English", "Русский" } },
            { "set.tutorial", new[] { "Replay tutorial", "Повторить обучение" } },
            { "set.reset", new[] { "Reset progress", "Сбросить прогресс" } },
            { "set.reset.confirm", new[] { "Start over? All progress will be lost.", "Начать заново? Весь прогресс пропадёт." } },
            { "set.yes", new[] { "Yes, reset", "Да, сбросить" } },
            { "set.no", new[] { "Cancel", "Отмена" } },
            { "set.on", new[] { "ON", "ВКЛ" } },
            { "set.off", new[] { "OFF", "ВЫКЛ" } },
            { "set.close", new[] { "Close", "Закрыть" } },

            // ---- popups ----
            { "offline.title", new[] { "Welcome back!", "С возвращением!" } },
            { "offline.text", new[] { "Your helpers earned while you were away", "Помощники заработали, пока тебя не было" } },
            { "offline.collect", new[] { "Collect", "Забрать" } },
            { "levelup.title", new[] { "Market level {0}!", "Рынок: уровень {0}!" } },
            { "levelup.reward", new[] { "Bonus +{0}", "Бонус +{0}" } },
            { "demo.title", new[] { "Demo complete!", "Демо пройдено!" } },
            { "demo.text", new[] { "You built the whole market! Quests, station stars and new levels go on.", "Весь рынок построен! Дальше — задания, звёзды станций и новые уровни." } },
            { "demo.stats", new[] { "Time: {0}   Customers: {1}   Earned: {2}", "Время: {0}   Покупателей: {1}   Заработано: {2}" } },
            { "demo.continue", new[] { "Keep playing", "Играть дальше" } },
            { "demo.restart", new[] { "Start over", "Начать заново" } },
            { "toast.new_area", new[] { "New area unlocked!", "Открыта новая зона!" } },
            { "toast.vip", new[] { "A VIP guest is here!", "Пришёл VIP-гость!" } },
            { "toast.hired", new[] { "New helper hired!", "Новый помощник!" } },
            { "nudge.wait", new[] { "Customers are on the way!", "Покупатели уже идут!" } },
            // ---- progression v2: levels, perk cards, stations, quests ----
            { "levelup.reward2", new[] { "+{0} coins  ·  +{1} medal", "+{0} монет  ·  +{1} медаль" } },
            { "levelup.pick", new[] { "Pick a bonus — it stays forever", "Выбери бонус — он останется навсегда" } },
            { "levelup.reroll", new[] { "Other cards", "Другие карточки" } },
            { "rarity.Common", new[] { "COMMON", "ОБЫЧНАЯ" } },
            { "rarity.Rare", new[] { "RARE", "РЕДКАЯ" } },
            { "rarity.Epic", new[] { "EPIC", "ЭПИЧЕСКАЯ" } },
            { "perk.Speed", new[] { "Quick feet", "Быстрые лапки" } },
            { "perk.Speed.d", new[] { "Walk speed {0}", "Скорость шага {0}" } },
            { "perk.Capacity", new[] { "Big basket", "Большая корзина" } },
            { "perk.Capacity.d", new[] { "Carry {0} more", "Носишь больше: {0}" } },
            { "perk.Profit", new[] { "Generous buyers", "Щедрые покупатели" } },
            { "perk.Profit.d", new[] { "All prices {0}", "Все цены {0}" } },
            { "perk.TreeGrow", new[] { "Fruitful trees", "Урожайные яблони" } },
            { "perk.TreeGrow.d", new[] { "Apple growth {0}", "Рост яблок {0}" } },
            { "perk.Bake", new[] { "Hot ovens", "Жаркие печи" } },
            { "perk.Bake.d", new[] { "Oven speed {0}", "Скорость печей {0}" } },
            { "perk.HelperSpeed", new[] { "Eager helpers", "Шустрые помощники" } },
            { "perk.HelperSpeed.d", new[] { "Helper speed {0}", "Скорость помощников {0}" } },
            { "perk.Patience", new[] { "Patient guests", "Терпеливые гости" } },
            { "perk.Patience.d", new[] { "Customer patience {0}", "Терпение покупателей {0}" } },
            { "perk.Tips", new[] { "Big tips", "Щедрые чаевые" } },
            { "perk.Tips.d", new[] { "Tips at tables {0}", "Чаевые за столиком {0}" } },
            { "perk.Vip", new[] { "Famous market", "Знаменитый рынок" } },
            { "perk.Vip.d", new[] { "VIP guests more often: {0}", "VIP-гости чаще: {0}" } },
            { "perk.Checkout", new[] { "Nimble till", "Ловкая касса" } },
            { "perk.Checkout.d", new[] { "Checkout speed {0}", "Скорость кассы {0}" } },
            { "chapter.title", new[] { "Chapter {0} complete!", "Глава {0} пройдена!" } },
            { "chapter.text", new[] { "Chest: +{0} coins and +{1} medals", "Сундук: +{0} монет и +{1} медали" } },
            { "tab.hero", new[] { "Me", "Героиня" } },
            { "tab.stations", new[] { "Stations", "Станции" } },
            { "st.Apples", new[] { "Apple trees", "Яблони" } },
            { "st.Bread", new[] { "Bakery oven", "Хлебная печь" } },
            { "st.Pies", new[] { "Pie oven", "Печь пирогов" } },
            { "st.Checkout", new[] { "Checkout", "Касса" } },
            { "st.desc", new[] { "Speed ×{0} · price ×{1}", "Быстрее ×{0} · цена ×{1}" } },
            { "st.desc.checkout", new[] { "Speed ×{0} · prices +{1}%", "Быстрее ×{0} · цены +{1}%" } },
            { "st.locked", new[] { "Opens with: {0}", "Откроется: {0}" } },
            { "st.soon", new[] { "Locked", "Закрыто" } },
            { "toast.star", new[] { "{0}: new star!", "{0}: новая звезда!" } },
            { "q.claim", new[] { "Claim!", "Забрать!" } },
            { "q.chapter", new[] { "Chapter {0} · {1}/{2}", "Глава {0} · {1}/{2}" } },
            { "q.more", new[] { "Market orders", "Заказы рынка" } },
            { "q.serve", new[] { "Serve {0} customers", "Обслужи покупателей: {0}" } },
            { "q.sell", new[] { "Sell {0} {1}", "Продай {0} {1}" } },
            { "q.items.Apple", new[] { "apples", "яблок" } },
            { "q.items.Bread", new[] { "loaves of bread", "буханок хлеба" } },
            { "q.items.Pie", new[] { "pies", "пирогов" } },
            { "q.items.Plate", new[] { "plates", "тарелок" } },
            { "q.vip", new[] { "Serve VIP guests: {0}", "Обслужи VIP-гостей: {0}" } },
            { "q.wash", new[] { "Wash {0} plates", "Помой тарелки: {0}" } },
            { "q.earn", new[] { "Collect {0} coins at the till", "Собери на кассе {0} монет" } },
            { "q.upgrade", new[] { "{0}: reach level {1}", "{0}: уровень {1}" } },
            { "q.station_lv", new[] { "{0}: upgrade to level {1}", "{0}: улучши до уровня {1}" } },
            { "q.station_star", new[] { "{0}: earn a star", "{0}: получи звезду" } },
            { "q.level", new[] { "Reach market level {0}", "Подними рынок до уровня {0}" } },
            { "q.tree2", new[] { "Plant a second apple tree", "Посади вторую яблоню" } },
            { "q.hire_cashier", new[] { "Hire a cashier", "Найми кассира" } },
            { "q.oven", new[] { "Build the bakery oven", "Построй хлебную печь" } },
            { "q.upgrades", new[] { "Put up the upgrade board", "Поставь доску улучшений" } },
            { "q.hire_picker", new[] { "Hire an apple picker", "Найми сборщика яблок" } },
            { "q.terrace", new[] { "Open the terrace", "Открой террасу" } },
            { "q.hire_baker", new[] { "Hire a baker", "Найми пекаря" } },
            { "q.oven2", new[] { "Build a second oven", "Построй вторую печь" } },
            { "q.hire_pie_helper", new[] { "Hire a terrace helper", "Найми помощника на террасу" } },
            { "tip.4", new[] { "Tap the quest card to see where to go", "Нажми на задание — покажу, куда идти" } },
            { "tip.5", new[] { "Medals from quests buy station stars", "Медали за задания — это звёзды для станций" } },

        };
    }
}
