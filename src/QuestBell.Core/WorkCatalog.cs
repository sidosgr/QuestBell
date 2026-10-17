namespace QuestBell.Core;

public record WorkTemplate(string Id, string En, string El, string Category, string Topic, string Repeat = "none")
{
    public string Label(string language) => language == "el" ? El : En;
}

public static class WorkCatalog
{
    public static readonly WorkTemplate[] Templates =
    [
        new("email-client", "Email the client", "Στείλε email στον πελάτη", "documents", "work-email"),
        new("call-client", "Call the client", "Κάλεσε τον πελάτη", "appointments", "work-email"),
        new("follow-up", "Follow up with the client", "Επικοινώνησε ξανά με τον πελάτη", "documents", "work-email"),
        new("send-quote", "Send a quotation", "Στείλε προσφορά", "documents", "work-office"),
        new("send-invoice", "Send an invoice", "Στείλε τιμολόγιο", "bills", "work-office"),
        new("check-payment", "Check client payment", "Έλεγξε την πληρωμή πελάτη", "bills", "work-office"),
        new("review-order", "Review order", "Έλεγξε την παραγγελία", "shopping", "work-shipping"),
        new("confirm-order", "Confirm order", "Επιβεβαίωσε την παραγγελία", "shopping", "work-shipping"),
        new("pack-order", "Pack order", "Συσκεύασε την παραγγελία", "shopping", "work-shipping"),
        new("ship-order", "Ship order", "Στείλε την παραγγελία", "shopping", "work-shipping"),
        new("track-shipment", "Check shipment delivery", "Έλεγξε την παράδοση αποστολής", "shopping", "work-shipping"),
        new("order-products", "Order products", "Παράγγειλε προϊόντα", "shopping", "work-shipping"),
        new("order-supplies", "Order supplies", "Παράγγειλε προμήθειες", "shopping", "work-office"),
        new("check-stock", "Check stock levels", "Έλεγξε τα αποθέματα", "shopping", "work-shipping"),
        new("contact-supplier", "Contact the supplier", "Επικοινώνησε με τον προμηθευτή", "appointments", "work-email"),
        new("write-blog", "Write a new blog post", "Γράψε νέα ανάρτηση στο blog", "documents", "work-blog"),
        new("review-blog", "Review a blog draft", "Έλεγξε το προσχέδιο του blog", "documents", "work-blog"),
        new("publish-blog", "Publish a blog post", "Δημοσίευσε ανάρτηση στο blog", "documents", "work-blog"),
        new("weekly-newsletter", "Send the weekly newsletter", "Στείλε το εβδομαδιαίο newsletter", "documents", "work-newsletter", "weekly"),
        new("draft-newsletter", "Write the newsletter", "Γράψε το newsletter", "documents", "work-newsletter"),
        new("social-post", "Publish a social media post", "Δημοσίευσε στα κοινωνικά δίκτυα", "documents", "work-social"),
        new("schedule-social", "Schedule social media posts", "Προγραμμάτισε αναρτήσεις κοινωνικών δικτύων", "documents", "work-social"),
        new("content-plan", "Plan next week's content", "Οργάνωσε το περιεχόμενο της επόμενης εβδομάδας", "documents", "work-social", "weekly"),
        new("weekly-report", "Prepare the weekly report", "Ετοίμασε την εβδομαδιαία αναφορά", "documents", "work-office", "weekly"),
        new("team-meeting", "Prepare for the team meeting", "Προετοιμάσου για τη συνάντηση ομάδας", "appointments", "work-office"),
        new("project-review", "Review project progress", "Έλεγξε την πρόοδο έργου", "documents", "work-office")
    ];
    public static WorkTemplate? Find(string? id) => Templates.FirstOrDefault(t => t.Id == id);
    public static string Title(WorkTemplate template, string reference, string language) => template.Label(language) + (string.IsNullOrWhiteSpace(reference) ? "" : " · " + reference.Trim());
}
