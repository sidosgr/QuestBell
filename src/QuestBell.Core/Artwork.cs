using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QuestBell.Core;

public record Illustration(string Category, int Variant, string Sheet, int Cell, string En, string El, string Topics = "", string Categories = "")
{
    public string Label(string language) => language == "el" ? El : En;
}

/// <summary>Stable variant numbers keep artwork pinned in older backups unchanged.</summary>
public static class Artwork
{
    private static readonly string[][] LegacyNames =
    [
        ["Coin treasury", "Θησαυρός νομισμάτων", "Golden telephone", "Χρυσό τηλέφωνο", "Electric lantern", "Ηλεκτρικό φανάρι", "Water jug", "Κανάτα νερού"],
        ["Release scissors", "Ψαλίδι απελευθέρωσης", "Unbound hourglass", "Ελεύθερη κλεψύδρα", "Account key", "Κλειδί λογαριασμού", "Butterfly music box", "Μουσικό κουτί πεταλούδας"],
        ["Market basket", "Καλάθι αγοράς", "Winged parcel", "Φτερωτό δέμα", "Merchant satchel", "Τσάντα εμπόρου", "Little market cart", "Μικρό καρότσι αγοράς"],
        ["Starlight clock", "Ρολόι αστεριών", "Cloud calendar", "Ημερολόγιο σύννεφο", "Planner tea", "Τσάι και ατζέντα", "Timekeeper owl", "Κουκουβάγια χρονοφύλακας"],
        ["Cozy cottage", "Ζεστό σπιτάκι", "Golden watering can", "Χρυσό ποτιστήρι", "Little toolbox", "Μικρή εργαλειοθήκη", "Fern keeper fox", "Αλεπού με φτέρη"],
        ["Violet passport", "Μοβ διαβατήριο", "Ribbon scroll", "Πάπυρος με κορδέλα", "Letter mailbox", "Γραμματοκιβώτιο", "Messenger bird", "Πουλί αγγελιοφόρος"],
        ["Adventure fox", "Αλεπού εξερευνητής", "Moon cat", "Γάτα του φεγγαριού", "Starlight jar", "Βάζο αστεριών", "Emerald dragon", "Σμαραγδένιος δράκος"]
    ];

    // New variant IDs are shared across families, start at 4 and are append-only.
    private static readonly Illustration[] New = BuildNew();
    public static readonly IReadOnlyList<Illustration> All = Catalog.CategoryIds.SelectMany((category, i) =>
        Enumerable.Range(0, 4).Select(cell => new Illustration(category, cell, category, cell, LegacyNames[i][cell * 2], LegacyNames[i][cell * 2 + 1])))
        .Concat(New).ToArray();
    public static int Total => All.Count;
    public static bool ValidVariant(int variant) => variant >= -1 && variant < New.Length + 4;
    public static Illustration Get(string category, int variant)
    {
        if (variant >= 4 && variant < New.Length + 4) return New[variant - 4];
        int family = Array.IndexOf(Catalog.CategoryIds, category);
        if (family < 0) family = 6;
        return All[family * 4 + Math.Clamp(variant, 0, 3)];
    }
    public static Illustration[] ForCategory(string category) => All.Where(a => a.Variant < 4 ? a.Category == category : a.Categories.Split(' ').Contains(category)).ToArray();

    private static readonly (string Topic, string Words)[] Keywords =
    [
        ("phone", "phone mobile telephone τηλεφων* κινητ*"), ("electricity", "electricity electric power ρευμα* ηλεκτρ* δεη"),
        ("water", "water νερ* υδρευσ*"), ("internet", "internet wifi broadband router διαδικτυ* ιντερνετ"),
        ("rent", "rent rental ενοικ*"), ("tax", "tax taxes φορο* εφορι*"),
        ("streaming", "streaming netflix spotify disney cinema music συνδρομητικ*"), ("gym", "gym fitness workout exercise γυμναστ* γυμναστικ*"),
        ("software", "software app apps application λογισμ* εφαρμογ*"), ("cloud", "cloud storage backup dropbox αποθηκευσ*"),
        ("doctor", "doctor medical hospital clinic γιατρ* ιατρ* νοσοκομ*"), ("dentist", "dentist dental tooth teeth οδοντ* δοντ*"),
        ("car", "car vehicle mechanic service αυτοκινητ* οχημ* συνεργει*"), ("air", "air purifier filter φιλτρ* αερ*"),
        ("plants", "plant plants garden watering φυτ* κηπ* ποτισ*"), ("passport", "passport διαβατηρ*"),
        ("driving", "driving licence license οδηγησ* διπλωμ*"), ("travel", "travel trip flight holiday ταξιδ* πτησ* διακοπ*"),
        ("food", "food grocery groceries supermarket τροφιμ* σουπερμαρκετ ψωνι*"), ("online", "parcel delivery package courier δεμα* παραδοσ* παραλαβ*"),
        ("family", "family birthday anniversary gift οικογεν* γενεθλ* επετει* δωρ*"), ("home", "home house household σπιτ* οικι*"),
        ("personal", "personal selfcare rest care προσωπ* ξεκουρασ* φροντιδ*")
    ];
    private static string Normalize(string text) => new string(text.ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    public static string Topic(Reminder task)
    {
        if (task.IsWork && WorkCatalog.Find(task.WorkTemplateId) is { } work) return work.Topic;
        if (task.IsWork)
        {
            string workText = Normalize(task.Title);
            foreach (var (topic, words) in new (string, string[])[] {
                ("work-newsletter", ["newsletter", "newsletters", "ενημερωτικο δελτιο"]),
                ("work-blog", ["blog", "article", "manuscript", "αρθρ", "προσχεδιο"]),
                ("work-social", ["social", "instagram", "facebook", "tiktok", "κοινωνικ"]),
                ("work-office", ["invoice", "supplies", "payment", "report", "review", "project", "τιμολογ", "προμηθει", "πληρωμ", "αναφορ", "εργο"]),
                ("work-shipping", ["ship", "parcel", "order", "stock", "παραγγελι", "αποστολ", "δεμα", "αποθεμα"]),
                ("work-email", ["email", "e-mail", "client", "supplier", "πελατ", "προμηθευτ"]) })
                if (words.Any(workText.Contains)) return topic;
        }
        if (!task.CustomTitle)
        {
            if (task.ObjectId == "parcel") return "online";
            if (task.ObjectId == "filter") return "air";
            if (Catalog.Subjects.Any(s => s.Id == task.SubjectId)) return task.SubjectId;
        }
        string text = Normalize(task.CustomTitle ? task.Title : task.Title + " " + task.SubjectId.TrimStart('='));
        var tokens = Regex.Split(text, @"[^\p{L}\p{N}]+").Where(t => t.Length > 0).ToArray();
        foreach (var (topic, words) in Keywords)
            if (words.Split(' ').Any(word => tokens.Any(token => word.EndsWith('*') ? token.StartsWith(word[..^1], StringComparison.Ordinal) : token == word))) return topic;
        return "";
    }
    public static int[] Candidates(Reminder task)
    {
        string topic = Topic(task);
        var options = ForCategory(task.Category);
        var matches = options.Where(a => a.Topics.Split(' ').Contains(topic) && topic.Length > 0).Select(a => a.Variant).ToArray();
        if (matches.Length == 0 && topic.Length > 0) matches = New.Where(a => a.Topics.Split(' ').Contains(topic)).Select(a => a.Variant).ToArray();
        return matches.Length > 0 ? matches : options.Where(a => a.Variant < 4).Select(a => a.Variant).DefaultIfEmpty(0).ToArray();
    }
    public static Illustration[] Choices(Reminder task) => Candidates(task).Select(v => Get(task.Category, v))
        .Concat(ForCategory(task.Category)).DistinctBy(a => a.Variant).ToArray();
    public static int Preferred(Reminder task) => Candidates(task)[0];
    public static int DisplayVariant(Reminder task) => task.ArtVariant >= 0 ? task.ArtVariant : Candidates(task).Contains(task.LastArtVariant) ? task.LastArtVariant : Preferred(task);
    public static int NextVariant(Reminder task, Random? random = null)
    {
        if (task.ArtVariant >= 0) return task.ArtVariant;
        var choices = Candidates(task);
        var fresh = choices.Where(v => v != task.LastArtVariant).ToArray();
        if (fresh.Length == 0) fresh = choices;
        return fresh[(random ?? Random.Shared).Next(fresh.Length)];
    }

    private static Illustration[] BuildNew()
    {
        var result = new List<Illustration>();
        void Pair(string sheet, int cell, string topics, string categories, string en, string el)
        { result.Add(new(categories.Split(' ')[0], 4 + result.Count, sheet, cell, en, el, topics, categories)); }
        Pair("phone-friends", 0, "phone", "bills subscriptions personal", "Pocket phone & bill", "Κινητό και λογαριασμός");
        Pair("phone-friends", 1, "phone", "bills subscriptions personal", "Phone fox", "Αλεπού με κινητό");
        Pair("phone-friends", 2, "phone", "bills subscriptions personal", "Coin-call telephone", "Τηλέφωνο με νομίσματα");
        Pair("phone-friends", 3, "phone", "bills subscriptions personal", "Invoice raccoon", "Ρακούν με λογαριασμό");
        Pair("connected-friends", 0, "internet", "bills subscriptions home", "Signal keeper router", "Δρομολογητής σήματος");
        Pair("connected-friends", 1, "internet", "bills subscriptions home", "Wi-Fi owl", "Κουκουβάγια Wi-Fi");
        Pair("connected-friends", 2, "cloud software", "bills subscriptions personal", "Cloud laptop", "Φορητός υπολογιστής cloud");
        Pair("connected-friends", 3, "cloud software", "bills subscriptions personal", "Cloud hedgehog", "Σκαντζόχοιρος cloud");
        Pair("utility-friends", 0, "electricity", "bills home", "Bright idea bulb", "Λαμπτήρας έμπνευσης");
        Pair("utility-friends", 1, "electricity", "bills home", "Lantern dragon", "Δράκος με φανάρι");
        Pair("utility-friends", 2, "water", "bills home", "Crystal water jug", "Κρυστάλλινη κανάτα νερού");
        Pair("utility-friends", 3, "water", "bills home", "Water keeper otter", "Βίδρα του νερού");
        Pair("money-friends", 0, "rent home", "bills home", "Keys to home", "Κλειδιά σπιτιού");
        Pair("money-friends", 1, "rent home", "bills home", "House key fox", "Αλεπού με κλειδιά");
        Pair("money-friends", 2, "tax", "bills documents", "Coin counter ledger", "Λογιστικό βιβλίο και αριθμομηχανή");
        Pair("money-friends", 3, "tax", "bills documents", "Accountant owl", "Κουκουβάγια λογιστής");
        Pair("subscription-friends", 0, "streaming", "subscriptions personal", "Final episode TV", "Τηλεόραση τελευταίου επεισοδίου");
        Pair("subscription-friends", 1, "streaming", "subscriptions personal", "Remote control cat", "Γάτα με τηλεχειριστήριο");
        Pair("subscription-friends", 2, "software", "subscriptions personal", "Account release laptop", "Υπολογιστής αποδέσμευσης λογαριασμού");
        Pair("subscription-friends", 3, "software", "subscriptions personal", "Unsubscribe raccoon", "Ρακούν ακύρωσης συνδρομής");
        Pair("wellbeing-friends", 0, "gym", "subscriptions appointments personal", "Workout kit", "Εξοπλισμός γυμναστικής");
        Pair("wellbeing-friends", 1, "gym", "subscriptions appointments personal", "Workout bunny", "Κουνελάκι γυμναστής");
        Pair("wellbeing-friends", 2, "personal", "personal", "Little self-care ritual", "Μικρή τελετουργία φροντίδας");
        Pair("wellbeing-friends", 3, "personal", "personal", "Tea-time panda", "Πάντα με τσάι");
        Pair("delivery-friends", 0, "food", "shopping personal", "Fresh grocery basket", "Καλάθι με φρέσκα τρόφιμα");
        Pair("delivery-friends", 1, "food", "shopping personal", "Grocery bunny", "Κουνελάκι με ψώνια");
        Pair("delivery-friends", 2, "online", "shopping personal", "Express winged parcel", "Φτερωτό δέμα εξπρές");
        Pair("delivery-friends", 3, "online", "shopping personal", "Courier corgi", "Κόργκι διανομέας");
        Pair("health-friends", 0, "doctor", "appointments personal", "Doctor's bag", "Ιατρική τσάντα");
        Pair("health-friends", 1, "doctor", "appointments personal", "Doctor bear", "Αρκουδάκι γιατρός");
        Pair("health-friends", 2, "dentist", "appointments personal", "Sparkling tooth", "Αστραφτερό δόντι");
        Pair("health-friends", 3, "dentist", "appointments personal", "Dentist beaver", "Κάστορας οδοντίατρος");
        Pair("motoring-friends", 0, "car", "home appointments bills shopping", "Little motor & wrench", "Αυτοκίνητο και κλειδί συνεργείου");
        Pair("motoring-friends", 1, "car", "home appointments bills shopping", "Mechanic fox", "Αλεπού μηχανικός");
        Pair("motoring-friends", 2, "driving car", "documents appointments home bills", "Road-ready keys", "Κλειδιά και άδεια οδήγησης");
        Pair("motoring-friends", 3, "driving car", "documents appointments home bills", "Driver hedgehog", "Σκαντζόχοιρος οδηγός");
        Pair("household-friends", 0, "air home", "home shopping", "Fresh-air filter", "Φίλτρο καθαρού αέρα");
        Pair("household-friends", 1, "air home", "home shopping", "Filter keeper cat", "Γάτα φροντίδας φίλτρου");
        Pair("household-friends", 2, "plants home", "home shopping", "Fern & watering can", "Φτέρη και ποτιστήρι");
        Pair("household-friends", 3, "plants home", "home shopping", "Gardener fox", "Αλεπού κηπουρός");
        Pair("travel-friends", 0, "passport travel", "documents personal", "Passport & luggage", "Διαβατήριο και αποσκευές");
        Pair("travel-friends", 1, "passport travel", "documents personal", "Passport penguin", "Πιγκουίνος με διαβατήριο");
        Pair("travel-friends", 2, "tax travel", "documents personal", "Sealed document stack", "Σφραγισμένα έγγραφα");
        Pair("travel-friends", 3, "tax travel", "documents personal", "Paperwork owl", "Κουκουβάγια εγγράφων");
        Pair("family-friends", 0, "family", "personal", "Family memory frame", "Κορνίζα οικογενειακών αναμνήσεων");
        Pair("family-friends", 1, "family", "personal", "Family corgi", "Οικογενειακό κόργκι");
        Pair("family-friends", 2, "family", "personal", "Celebration calendar", "Ημερολόγιο γιορτής");
        Pair("family-friends", 3, "family personal", "personal", "Heart keeper cat", "Γάτα με καρδιά");
        Pair("work-email-friends", 0, "work-email", "documents appointments", "Enchanted email inbox", "Μαγικό εισερχόμενο email");
        Pair("work-email-friends", 1, "work-email", "documents appointments", "Red panda mail messenger", "Κόκκινο πάντα αγγελιοφόρος");
        Pair("work-email-friends", 2, "work-email", "documents appointments", "Golden-envelope laptop", "Υπολογιστής με χρυσό φάκελο");
        Pair("work-email-friends", 3, "work-email", "documents appointments", "Snowy email owl", "Χιονισμένη κουκουβάγια email");
        Pair("work-shipping-friends", 0, "work-shipping", "shopping documents", "Order dispatch trolley", "Καρότσι αποστολής παραγγελιών");
        Pair("work-shipping-friends", 1, "work-shipping", "shopping documents", "Goggle courier corgi", "Κόργκι διανομέας με γυαλιά");
        Pair("work-shipping-friends", 2, "work-shipping", "shopping documents", "Golden parcel delivery van", "Φορτηγάκι χρυσών δεμάτων");
        Pair("work-shipping-friends", 3, "work-shipping", "shopping documents", "Raccoon order dispatcher", "Ρακούν υπεύθυνος αποστολών");
        Pair("work-blog-friends", 0, "work-blog", "documents personal", "Storymaker typewriter", "Γραφομηχανή ιστοριών");
        Pair("work-blog-friends", 1, "work-blog", "documents personal", "Fox blog writer", "Αλεπού συγγραφέας blog");
        Pair("work-blog-friends", 2, "work-blog", "documents personal", "Article studio laptop", "Υπολογιστής συγγραφής άρθρων");
        Pair("work-blog-friends", 3, "work-blog", "documents personal", "Hedgehog manuscript editor", "Σκαντζόχοιρος επιμελητής κειμένων");
        Pair("work-newsletter-friends", 0, "work-newsletter", "documents personal", "Newsletter printing press", "Τυπογραφείο newsletter");
        Pair("work-newsletter-friends", 1, "work-newsletter", "documents personal", "Bunny newsletter courier", "Κουνελάκι διανομέας newsletter");
        Pair("work-newsletter-friends", 2, "work-newsletter", "documents personal", "Weekly dispatch bundle", "Πακέτο εβδομαδιαίας ενημέρωσης");
        Pair("work-newsletter-friends", 3, "work-newsletter", "documents personal", "Penguin newsletter editor", "Πιγκουίνος συντάκτης newsletter");
        Pair("work-social-friends", 0, "work-social", "documents personal", "Social studio smartphone", "Κινητό δημιουργίας περιεχομένου");
        Pair("work-social-friends", 1, "work-social", "documents personal", "Cat content creator", "Γάτα δημιουργός περιεχομένου");
        Pair("work-social-friends", 2, "work-social", "documents personal", "Camera & content calendar", "Κάμερα και ημερολόγιο περιεχομένου");
        Pair("work-social-friends", 3, "work-social", "documents personal", "Dragon community announcer", "Δράκος ανακοινώσεων κοινότητας");
        Pair("work-office-friends", 0, "work-office", "shopping documents bills appointments", "Golden stationery caddy", "Χρυσή θήκη γραφικής ύλης");
        Pair("work-office-friends", 1, "work-office", "shopping documents bills appointments", "Beaver supplies keeper", "Κάστορας υπεύθυνος προμηθειών");
        Pair("work-office-friends", 2, "work-office", "shopping documents bills appointments", "Invoice ledger & calculator", "Βιβλίο τιμολογίων και αριθμομηχανή");
        Pair("work-office-friends", 3, "work-office", "shopping documents bills appointments", "Owl project reviewer", "Κουκουβάγια ελεγκτής έργων");
        return result.ToArray();
    }
}
