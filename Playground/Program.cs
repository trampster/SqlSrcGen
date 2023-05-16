using SqlSrcGen;
using SqlSrcGen.Runtime;

string databaseName = "database.sql";
if (File.Exists(databaseName))
{
    File.Delete(databaseName);
}

var database = new Database(databaseName);
database.CreateContactTable();

database.InsertContact(new Contact() { Name = "Bob", Email = "bob@marley.com", Age = 12, Height = 167.8, PrivateKey = new byte[] { 1, 2, 3, 4 }, Mana = new Numeric(24.4d) });

var list = new List<Contact>();
database.AllContacts(list);

foreach (var contact in list)
{
    Console.WriteLine($"Name: {contact.Name} Email: {contact.Email} Age: {contact.Age} Height: {contact.Height} PrivateKey: {string.Join(',', contact.PrivateKey)}");
    Console.WriteLine($"Mana: {contact.Mana.GetReal()}");
}





