# <img src="Icon/SqlSrcGen.svg" width="32">  SqlSrcGen
SqlSrcGen is a SQL first, reflection free micro ORM for SQLite using c# source generators.
The class definitions and Object Relational Mappings are created automatically from your SQL CREATE TABLE commands.

## Advantages
* No need to manually define c# classes for your tables
* High performance - mapping code is reflection free and optimized at compile time
* SQL code is compile time checked
* AOT friendly - no reflection 

## Getting Started
1. Create a .sql file in your project which includes the CREATE TABLE SQL commands defining your database.
```sql
CREATE TABLE contact (name Text not null primary key, email Text not null);
```

2. Include that .sql file as AdditionalFiles in your .csproj

```xml
<ItemGroup>
    <AdditionalFiles Include="SqlSchema.sql" />
</ItemGroup>
```
3. Do crud operations on the tables

```c#
var database = new Database(databaseName);

// create the table
database.CreateContactTable();

// insert a record
database.InsertContact(new Contact() 
{ 
    Name = "Steve Rogers", 
    Email = "steve@avengers.com"
});

// query all records in the table
var list = new List<Contact>();
database.AllContacts(list);

// get row via primary key, (only generated for tables with a primary key)
var contact = new Contact();
bool found = database.GetContact(contact, contact);

// delete all rows from table
database!.DeleteAllContacts();

// delete a row vis primary key (only generated for tables with a primary key)
database!.DeleteContact("Steve Rogers");
```

## Future Work
SqlSrcGen currently only supports basic crud operations generated directly from sql table definitions. Future features include:
* Custom queries (select, joins etc)
* transactions
