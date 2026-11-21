using System;
using System.Collections.Generic;
using DialogueEditor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoBA.Settings
{
    /// <summary>
    /// Централизованный словарь локализации (English / Русский) для меню, HUD, квестов,
    /// подсказок взаимодействия, экрана смерти и всех диалогов DialogueEditor.
    /// </summary>
    public static class GameLocalization
    {
        private static readonly Dictionary<string, string> EnToRu = new(StringComparer.OrdinalIgnoreCase)
        {
            // Главное меню и панели
            ["PATIENT RECORD // 04 — SAYA"] = "ЛИЧНОЕ ДЕЛО // 04 — САЯ",
            ["she is still out there"] = "она всё ещё там",
            ["headphones recommended. play in the dark."] = "рекомендуются наушники. играйте в темноте.",
            ["CONTINUE"] = "ПРОДОЛЖИТЬ",
            ["SAVE GAME"] = "СОХРАНИТЬ",
            ["LOAD GAME"] = "ЗАГРУЗИТЬ",
            ["NEW GAME"] = "НОВАЯ ИГРА",
            ["CHAPTERS"] = "ГЛАВЫ",
            ["SETTINGS"] = "НАСТРОЙКИ",
            ["CONTROLS"] = "УПРАВЛЕНИЕ",
            ["QUIT"] = "ВЫХОД",
            ["BACK"] = "НАЗАД",
            ["SENSITIVITY"] = "ЧУВСТВИТЕЛЬНОСТЬ",
            ["LANGUAGE"] = "ЯЗЫК",
            ["ENGLISH"] = "ENGLISH",
            ["RUSSIAN"] = "РУССКИЙ",

            // Главы
            ["I.   THE CRASH"] = "I.   АВАРИЯ",
            ["A wet road. Headlights. Then nothing."] = "Мокрая дорога. Фары. И темнота.",
            ["II.  THE HOSPITAL"] = "II.  БОЛЬНИЦА",
            ["You wake up. She is not here."] = "Ты просыпаешься. Её здесь нет.",
            ["III. THE FOREST"] = "III. ЛЕС",
            ["Find Saya. Do not let them see you."] = "Найди Саю. Не дай им себя увидеть.",

            // Управление
            ["W A S D"] = "W A S D",
            ["MOUSE"] = "МЫШЬ",
            ["SHIFT"] = "SHIFT",
            ["CTRL"] = "CTRL",
            ["SPACE"] = "ПРОБЕЛ",
            ["E"] = "E",
            ["1 / 2"] = "1 / 2",
            ["ESC"] = "ESC",
            ["move"] = "движение",
            ["look around"] = "обзор",
            ["run"] = "бег",
            ["crouch"] = "присесть",
            ["jump"] = "прыжок",
            ["interact"] = "взаимодействие",
            ["previous / next"] = "пред. / след.",
            ["back to menu"] = "назад в меню",

            // Пауза
            ["PAUSED"] = "ПАУЗА",
            ["PAUSE"] = "ПАУЗА",
            ["RESUME"] = "ПРОДОЛЖИТЬ",
            ["SAVE"] = "СОХРАНИТЬ",
            ["LOAD"] = "ЗАГРУЗИТЬ",
            ["MAIN MENU"] = "ГЛАВНОЕ МЕНЮ",
            ["MENU"] = "МЕНЮ",
            ["EXIT"] = "ВЫЙТИ",

            // Взаимодействие и HUD
            ["Press E to Interact"] = "Нажмите E, чтобы взаимодействовать",
            ["Press E to talk"] = "Нажмите E, чтобы поговорить",
            ["Game Saved"] = "Игра сохранена",
            ["You died..."] = "Вы погибли...",
            ["IT FOUND YOU"] = "ОН НАШЁЛ ТЕБЯ",

            // Задачи квеста и надписи на сцене
            ["Find lake."] = "Найти озеро.",
            ["Find lake"] = "Найти озеро",
            ["Lake"] = "Озеро",
            ["Play"] = "Играть",
            ["End"] = "Закончить",
            ["End Conversation"] = "Закончить разговор",
            ["Find Saya"] = "Найти Саю",
            ["Follow Saya"] = "Следовать за Саей",
            ["Talk to Saya"] = "Поговорить с Саей",
            ["Find the key"] = "Найти ключ",
            ["Enter the house"] = "Зайти в дом",
            ["Go to the grocery store"] = "Дойти до продуктового",
            ["Take from the shopkeeper: vodka, 3 apples, bread"] = "Взять у продавца: водка, 3 яблока, хлеб",
            ["Return to Saya"] = "Вернуться к Сае",
            ["Sit at the table"] = "Сесть за стол",

            // Имена спикеров
            ["You"] = "Ты",
            ["Saya"] = "Сая",
            ["Shopkeeper"] = "Продавец",
            ["Valera-kun"] = "Валера-кун",

            // Диалоги CarCrash и Hospital
            ["Hello? Yes, I'm already on my way, I'll be there soon"] =
                "Алло, да, уже еду, скоро буду",
            ["GET AWAY FROM ME, CREATURE"] =
                "ОТОЙДИ ОТ МЕНЯ, ТВАРЬ",
            ["GET THIS AWAY FROM ME"] =
                "УБЕРИТЕ ЭТО ОТ МЕНЯ",
            ["The creature left, but I still didn't understand where I was, or why everything was covered in blood"] =
                "Тварь ушла, но я до сих пор не понимал где я, почему все в крови",
            ["After lying in a mental hospital for a month, I realized the best decision would be to ignore my perception of the world"] =
                "Пролежав месяц в психушке я понял, что лучшим решением будет игнорировать мое восприятие мира",

            // Дополнительный диалог Саи в Level1
            ["Well, shall we go?"] = "Ну что, пошли?",

            // Диалоги (Saya Intro)
            ["You... you came back for me. Thank you... I thought the dark out there had taken you too."] =
                "Ты... ты вернулся за мной. Спасибо... Я думала, эта темнота забрала и тебя.",
            ["Catch your breath for a second... My legs are still shaking after what dragged me toward the lake."] =
                "Переведи дух... Ноги до сих пор дрожат после того, как что-то потащило меня к озеру.",
            ["Look around... there is not a scrap of food or clean water left in this house."] =
                "Посмотри вокруг... в этом доме не осталось ни крошки еды и ни капли чистой воды.",
            ["I cannot walk back out into those woods alone... If we stay hungry like this, we will not last the night."] =
                "Я не смогу одна выйти обратно в этот лес... Если мы останемся без еды, эту ночь мы не переживём.",
            ["Past the fence, down by the lake, there is an old gray building... an abandoned grocery store."] =
                "За забором, внизу у озера, есть старое серое здание... заброшенный продуктовый магазин.",
            ["Please, go there and bring back whatever food is still untouched — something to keep us going."] =
                "Пожалуйста, сходи туда и принеси всё, что ещё не тронуто — хоть что-то, чтобы продержаться.",
            ["Wait... the iron gate outside is locked. Nobody has used that shop since the mist swallowed the road."] =
                "Постой... железные ворота снаружи заперты. В тот магазин никто не заходил с тех пор, как туман накрыл дорогу.",
            ["I hid the spare gate key inside the chest of drawers right here in this house. Check the drawers carefully."] =
                "Я спрятала запасной ключ от ворот в одном из комодов прямо здесь, в доме. Внимательно проверь ящики.",
            ["Keep your voice down once you step outside. Do not wander near the shore, and do not let \"them\" hear you."] =
                "Когда выйдешь наружу, не шуми. Не подходи близко к берегу и не дай \"им\" себя услышать.",
            ["Find the key in the drawer, reach that gray store, and hurry back to me. Please."] =
                "Найди ключ в комоде, доберись до серого магазина и поскорее возвращайся ко мне. Пожалуйста.",

            // Диалог (Saya Repeat)
            ["Did you find the key in the chest of drawers? Please, hurry to that gray store past the gate... and be careful out there."] =
                "Ты уже нашёл ключ в комоде? Пожалуйста, поторопись в тот серый магазин за воротами... и будь осторожен.",
            ["Please hurry to the gray store... and stay out of sight."] =
                "Пожалуйста, поторопись в серый магазин... и не попадайся им на глаза.",
            ["Did you check the chest of drawers in this house? I hid the spare gate key inside one of them... please hurry."] =
                "Ты проверил комоды в этом доме? Я спрятала запасной ключ от ворот в одном из них... пожалуйста, поторопись.",
            ["You found the key! Hurry to the gray grocery store past the gate and get the food... please be careful out there."] =
                "Ты нашёл ключ! Поторопись в серый продуктовый магазин за воротами и принеси еды... пожалуйста, будь осторожен.",
            ["Please hurry back with the vodka, three apples, and bread... I will wait right here."] =
                "Пожалуйста, возвращайся поскорее с водкой, тремя яблоками и хлебом... Я буду ждать прямо здесь.",

            // Диалоги (Monster Shopkeeper)
            ["...Oh. A customer... How wonderfully... unexpected."] =
                "...О. Покупатель... Как удивительно... и неожиданно.",
            ["The gate hinges screamed... yet you still stepped inside. Nobody has crossed my threshold in... oh, so many winters..."] =
                "Петли ворот скрипели... но ты всё равно вошёл. Никто не переступал мой порог уже... столько зим...",
            ["Do not mind the dust... or the quiet. Tell me, traveler... what brings a living voice to my counter?"] =
                "Не обращай внимания на пыль... и на тишину. Скажи, путник... что привело живой голос к моему прилавку?",
            ["We're trapped in the house across the woods. Saya needs supplies — vodka, three apples, and a loaf of bread."] =
                "Мы застряли в доме за лесом. Сае нужны припасы — водка, три яблока и буханка хлеба.",
            ["...Vodka. Three apples. Bread... Such ordinary, fragile little things for a night like this..."] =
                "...Водка. Три яблока. Хлеб... Такие обычные, хрупкие вещи для такой-то ночи...",
            ["She is waiting for you, isn't she...? Listening at the floorboards... counting every breath until the door opens..."] =
                "Она ведь ждёт тебя, да...? Прислушивается к половицам... считает каждый вдох, пока не откроется дверь...",
            ["How much do we owe you? I don't have any money left on me."] =
                "Сколько я должен? У меня совсем не осталось денег.",
            ["Money...? No, no... Paper and copper mean very little when the road outside forgets where it leads..."] =
                "Деньги...? Нет, нет... Бумажки и медь ничего не значат, когда дорога снаружи забывает, куда ведёт...",
            ["...Consider it free for now... You will pay me another night... with the story of how long you both lasted."] =
                "...Считай, что пока бесплатно... Заплатишь мне в другую ночь... историей о том, сколько вы вдвоём продержались.",
            ["Wait right there... One bottle of vodka, kept cold behind the counter... Three apples, still firm enough to bite..."] =
                "Погоди немного... Одна бутылка водки, холодная, из-под прилавка... Три яблока, всё ещё крепкие...",
            ["...And a loaf of bread. Let me set them out on the counter for you..."] =
                "...И буханка хлеба. Сейчас я выложу всё на прилавок...",
            ["Take everything from the counter, traveler... Every last piece... Before the mist grows curious about us both."] =
                "Забирай всё с прилавка, путник... Всё до крошки... Пока туман не заинтересовался нами обоими.",
            ["...Go on back to her now. And do mind the dark... I would hate to lose my favorite guest..."] =
                "...А теперь ступай обратно к ней. И берегись темноты... Не хотелось бы потерять любимого гостя...",

            // Диалог (Monster Repeat)
            ["...You already have my wares... Go on, hurry back to her... Come again soon..."] =
                "...Ты уже забрал мой товар... Иди, поторопись к ней... Заходи ещё...",
            ["...You already have my wares... Go on, hurry back to her..."] =
                "...Ты уже забрал мой товар... Иди, поторопись к ней...",

            // Диалоги (Saya Final)
            ["You made it back... Every time the wind hit the porch I feared the worst."] =
                "Ты вернулся... Каждый раз, когда ветер бил в крыльцо, я боялась худшего.",
            ["Vodka, apples, bread... Thank you. You actually went through all of that for us."] =
                "Водка, яблоки, хлеб... Спасибо. Ты правда прошёл через всё это ради нас.",
            ["Leave them by the table and rest a moment... At least tonight, we are still alive."] =
                "Положи всё на стол и немного отдохни... По крайней мере этой ночью мы ещё живы.",

            // Диалог ужина (DinnerAfterFoodConversation)
            ["Wait... why aren't you eating? You said you were starving."] =
                "Подожди... почему ты не ешь? Ты же говорила, что умираешь с голоду.",
            ["...I'm not hungry anymore. Finish your food — you need your strength."] =
                "...Я больше не голодна. Доедай — тебе понадобятся силы.",
            ["I risked my life out there for this. Why won't you touch a single bite?"] =
                "Я жизнью рисковал там ради этого. Почему ты даже не притронулась к еде?",
            ["...Don't ask questions you don't want answered. Just rest for a moment."] =
                "...Не задавай вопросов, на которые не хочешь знать ответа. Просто отдохни немного."
        };

        private static readonly Dictionary<string, string> RuToEn = BuildReverseMap();

        private static readonly string[] WhispersEn =
        {
            "saya?", "don't look back", "they can hear you", "it wasn't your fault",
            "wake up", "she is waiting", "keep your lights off", "who was driving?",
        };

        private static readonly string[] WhispersRu =
        {
            "сая?", "не оглядывайся", "они тебя слышат", "ты не виноват",
            "проснись", "она ждёт", "выключи свет", "кто был за рулём?",
        };

        private static Dictionary<string, string> BuildReverseMap()
        {
            var rev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in EnToRu)
            {
                if (!rev.ContainsKey(kvp.Value))
                {
                    rev[kvp.Value] = kvp.Key;
                }
            }
            return rev;
        }

        public static bool IsRussian => GameSettingsService.Language == GameLanguage.Russian;

        public static string Get(string englishText)
        {
            if (string.IsNullOrEmpty(englishText)) return englishText;
            if (!IsRussian) return englishText;
            return EnToRu.TryGetValue(englishText, out string ru) ? ru : englishText;
        }

        public static string TranslateAny(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string trimmed = text.Trim();
            if (IsRussian)
            {
                return EnToRu.TryGetValue(trimmed, out string ru) ? ru : text;
            }

            return RuToEn.TryGetValue(trimmed, out string en) ? en : text;
        }

        public static string FormatGroceryCounter(int collected, int total)
        {
            return IsRussian
                ? $"Продукты: {collected}/{total}"
                : $"Groceries: {collected}/{total}";
        }

        public static string[] GetWhispers() => IsRussian ? WhispersRu : WhispersEn;

        public static void TranslateConversation(Conversation conversation)
        {
            if (conversation == null || conversation.Root == null) return;

            var visited = new HashSet<ConversationNode>();
            var queue = new Queue<ConversationNode>();
            queue.Enqueue(conversation.Root);
            visited.Add(conversation.Root);

            while (queue.Count > 0)
            {
                ConversationNode node = queue.Dequeue();
                if (node is SpeechNode speech)
                {
                    speech.Name = TranslateAny(speech.Name);
                    speech.Text = TranslateAny(speech.Text);
                }
                else if (node is OptionNode option)
                {
                    option.Text = TranslateAny(option.Text);
                }

                if (node.Connections == null) continue;
                for (int i = 0; i < node.Connections.Count; i++)
                {
                    Connection conn = node.Connections[i];
                    ConversationNode next = null;
                    if (conn is SpeechConnection sc) next = sc.SpeechNode;
                    else if (conn is OptionConnection oc) next = oc.OptionNode;

                    if (next != null && visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        public static void TranslateLoadedSceneTexts()
        {
            TMP_Text[] tmpTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text t = tmpTexts[i];
                if (t != null && !string.IsNullOrEmpty(t.text))
                {
                    string translated = TranslateAny(t.text);
                    if (translated != t.text) t.text = translated;
                }
            }

            Text[] uiTexts = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include);
            for (int i = 0; i < uiTexts.Length; i++)
            {
                Text t = uiTexts[i];
                if (t != null && !string.IsNullOrEmpty(t.text))
                {
                    string translated = TranslateAny(t.text);
                    if (translated != t.text) t.text = translated;
                }
            }
        }
    }
}
