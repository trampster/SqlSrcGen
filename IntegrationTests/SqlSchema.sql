CREATE TABLE contact (name Text not null, email Text not null, age Integer not null, height Real not null, privateKey Blob not null, mana Numeric not null);

CREATE TABLE nullable_contact (name Text, email Text, age Integer, height Real, privateKey Blob, mana Numeric);

CREATE TABLE primary_key_table (name Text primary key, email Text);

CREATE TABLE autoincrement_table (id INTEGER PRIMARY KEY AUTOINCREMENT, email TEXT);

CREATE TABLE autoincrement_not_null_table (id INTEGER PRIMARY KEY AUTOINCREMENT, email TEXT); 
