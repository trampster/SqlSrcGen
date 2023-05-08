// See https://aka.ms/new-console-template for more information
// Console.WriteLine("Hello, World!");

// if (File.Exists("database.sql"))
// {
//     File.Delete("database.sql");
// }

// var sqlite = new Sqlite("database.sql");

// sqlite.Query("CREATE TABLE contact (name Text, email Text);");

// sqlite.InsertContact(new Contact() { Name = "Luke", Email = "luke@rebels.com" });

// var list = new List<Contact>();

// for (int index = 0; index < 100000; index++)
// {
//     sqlite.QueryContact("SELECT * FROM contact;", list);
// }
//  
// Console.WriteLine($"Name: {list[0].Name}, Email: {list[0].Email}");


using SqlSrcGen;

// var schema = new Schema();
// Console.WriteLine(schema.SqlSchema);

// var contact = new Contact();
// contact.Name = "Daniel";
// contact.Email = "trampster@gmail.com";
// Console.WriteLine($"Name: {contact.Name} Email: {contact.Email}");


// var sqlite = new SqlSrcGen.Runtime.Sqlite("otherDatabase.sql");
// sqlite.CreateContactTable();

//sqlite.Query("CREATE TABLE contact (name Text, email Text);");  

string databaseName = "database.sql";
if (File.Exists(databaseName))
{
    File.Delete(databaseName);
}

var database = new Database(databaseName);
database.CreateContactTable();

database.InsertContact(new Contact() { Name = "Bob", Email = "bob@marley.com", Age = 12 });

var list = new List<Contact>();
database.AllContacts(list);

foreach (var contact in list)
{
    Console.WriteLine($"Name: {contact.Name} Email: {contact.Email} Age: {contact.Age}");
}

//List<Contact> contacts = new List<Contact>();
//database.AllContacts(contacts);





