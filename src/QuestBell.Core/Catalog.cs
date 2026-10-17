namespace QuestBell.Core;

public record Phrase(string Id, string En, string El)
{
    public string Label(string language) => language == "el" ? El : En;
}
public record Template(string Id, string Category, string Action, string Subject, string Object);
public record Delay(int Value, string Unit);

public static class Catalog
{
    public static readonly string[] CategoryIds = ["bills", "subscriptions", "shopping", "appointments", "home", "documents", "personal"];
    public static readonly Phrase[] Categories =
    [new("bills", "Bills & payments", "Λογαριασμοί & πληρωμές"), new("subscriptions", "Subscriptions", "Συνδρομές"),
     new("shopping", "Shopping & deliveries", "Αγορές & παραδόσεις"), new("appointments", "Appointments", "Ραντεβού"),
     new("home", "Home & maintenance", "Σπίτι & συντήρηση"), new("documents", "Documents & renewals", "Έγγραφα & ανανεώσεις"),
     new("personal", "Personal tasks", "Προσωπικά")];
    public static readonly Phrase[] Actions =
    [new("pay", "Pay", "Πλήρωσε"), new("cancel", "Cancel", "Ακύρωσε"), new("renew", "Renew", "Ανανέωσε"),
     new("book", "Book", "Κλείσε"), new("buy", "Buy", "Αγόρασε"), new("collect", "Collect", "Παράλαβε"),
     new("check", "Check", "Έλεγξε"), new("replace", "Replace", "Άλλαξε"), new("submit", "Submit", "Υπέβαλε"),
     new("plan", "Plan", "Οργάνωσε")];
    public static readonly Phrase[] Subjects =
    [new("phone", "phone", "τηλεφώνου"), new("electricity", "electricity", "ηλεκτρικού ρεύματος"),
     new("water", "water", "νερού"), new("internet", "internet", "διαδικτύου"), new("rent", "rent", "ενοικίου"),
     new("tax", "tax", "φόρου"), new("streaming", "streaming", "streaming"), new("gym", "gym", "γυμναστηρίου"),
     new("software", "software", "λογισμικού"), new("cloud", "cloud storage", "αποθήκευσης cloud"),
     new("doctor", "doctor", "γιατρού"), new("dentist", "dentist", "οδοντιάτρου"), new("car", "car", "αυτοκινήτου"),
     new("home", "home", "σπιτιού"), new("air", "air", "αέρα"), new("plants", "plant", "φυτών"),
     new("passport", "passport", "διαβατηρίου"), new("driving", "driving", "οδήγησης"),
     new("travel", "travel", "ταξιδιού"), new("food", "food", "τροφίμων"), new("online", "online", "ηλεκτρονικής αγοράς"),
     new("family", "family", "οικογένειας"), new("personal", "personal", "προσωπικής φροντίδας")];
    public static readonly Phrase[] Objects =
    [new("bill", "bill", "λογαριασμό"), new("subscription", "subscription", "συνδρομή"), new("account", "account", "λογαριασμό χρήστη"),
     new("appointment", "appointment", "ραντεβού"), new("supplies", "supplies", "προμήθειες"), new("parcel", "parcel", "δέμα"),
     new("insurance", "insurance", "ασφάλεια"), new("license", "license", "άδεια"), new("filter", "filter", "φίλτρο"),
     new("documents", "documents", "έγγραφα"), new("care", "care", "φροντίδα"), new("task", "task", "εργασία")];
    public static readonly Template[] Templates =
    [new("phone-bill", "bills", "pay", "phone", "bill"), new("power-bill", "bills", "pay", "electricity", "bill"),
     new("water-bill", "bills", "pay", "water", "bill"), new("internet-bill", "bills", "pay", "internet", "bill"),
     new("rent-bill", "bills", "pay", "rent", "bill"), new("tax-bill", "bills", "pay", "tax", "bill"),
     new("cancel-stream", "subscriptions", "cancel", "streaming", "subscription"), new("cancel-gym", "subscriptions", "cancel", "gym", "subscription"),
     new("renew-cloud", "subscriptions", "renew", "cloud", "subscription"), new("cancel-software", "subscriptions", "cancel", "software", "subscription"),
     new("food-shop", "shopping", "buy", "food", "supplies"), new("parcel", "shopping", "collect", "online", "parcel"),
     new("home-shop", "shopping", "buy", "home", "supplies"), new("doctor", "appointments", "book", "doctor", "appointment"),
     new("dentist", "appointments", "book", "dentist", "appointment"), new("car-book", "appointments", "book", "car", "appointment"),
     new("air-filter", "home", "replace", "air", "filter"), new("plants", "home", "plan", "plants", "care"),
     new("car-check", "home", "check", "car", "insurance"), new("passport", "documents", "renew", "passport", "documents"),
     new("driving", "documents", "renew", "driving", "license"), new("tax-doc", "documents", "submit", "tax", "documents"),
     new("travel", "personal", "plan", "travel", "documents"), new("family", "personal", "plan", "family", "task"),
     new("personal", "personal", "plan", "personal", "care")];

    public static List<Delay> Delays =>
        new[] { 5, 10, 15, 30, 45 }.Select(n => new Delay(n, "minutes"))
        .Concat(new[] { 1, 2, 3, 6, 12 }.Select(n => new Delay(n, "hours")))
        .Concat(new[] { 1, 2, 3, 5 }.Select(n => new Delay(n, "days")))
        .Concat(new[] { 1, 2, 3 }.Select(n => new Delay(n, "weeks")))
        .Concat(new[] { 1, 3, 6, 12 }.Select(n => new Delay(n, "months"))).ToList();

    public static string Label(string kind, string id, string language)
    {
        if (id.StartsWith('=')) return id[1..];
        var list = kind switch { "action" => Actions, "subject" => Subjects, "object" => Objects, _ => Categories };
        return list.FirstOrDefault(p => p.Id == id)?.Label(language) ?? id;
    }
    public static string Title(string action, string subject, string obj, string lang)
    {
        var a = Label("action", action, lang); var s = Label("subject", subject, lang); var o = Label("object", obj, lang);
        return lang == "el" ? $"{a} {o} {s}" : $"{a} {s} {o}";
    }
    public static string Title(Reminder task, string lang) => task.IsWork && !task.CustomTitle && WorkCatalog.Find(task.WorkTemplateId) is { } work ? WorkCatalog.Title(work, task.WorkReference, lang) : task.CustomTitle ? task.Title : Title(task.ActionId, task.SubjectId, task.ObjectId, lang);
    public static string Title(Template t, string lang) => Title(t.Action, t.Subject, t.Object, lang);
    public static string MatchCategory(string action, string subject, string obj, string title = "")
    {
        if (action == "cancel" || obj is "subscription" or "account") return "subscriptions";
        if (action == "pay" || obj == "bill") return "bills";
        if (obj == "appointment") return "appointments";
        if (action is "buy" or "collect" || obj == "parcel") return "shopping";
        if (obj is "documents" or "license" || subject == "passport") return "documents";
        if (subject is "home" or "plants" or "air" || obj == "filter") return "home";
        var lower = title.ToLowerInvariant();
        foreach (var (cat, words) in new (string, string[])[] {
            ("subscriptions", ["cancel", "subscription", "ακύρ", "ακυρ", "συνδρομ"]),
            ("bills", ["bill", "pay", "rent", "πλήρω", "πληρω", "λογαριασ", "ενοικ"]),
            ("shopping", ["shop", "parcel", "buy", "αγορ", "παράλα", "παραλα"]),
            ("appointments", ["appointment", "doctor", "dentist", "ραντεβ", "γιατρ"]),
            ("documents", ["renew", "passport", "license", "διαβατ", "ανανέ", "ανανε", "έγγραφ"]),
            ("home", ["filter", "plant", "maintenance", "φίλτρ", "φυτ", "συντήρ"]) })
            if (words.Any(lower.Contains)) return cat;
        return "personal";
    }
}
