# XMLcompare
cross-platform .NET 5 console app that compares 2 XML files. it identifies and reports differences in the structure (tables + columns) and content (rows) of the XML datasets.

# prereqs
- .NET 5 SDK: download it here: https://dotnet.microsoft.com/download/dotnet/5.0

# installation
- clone
```sh 
git clone <repo-url>
```
- navigate to directory
```sh
cd <path>
```

# building
- run the following command in the project directory to build the app
```sh
dotnet build
```

# running
run it by using `dotnet run`, followed by the necessary args

# windows
```sh
dotnet run --project .\XMLcompare\XMLcompare.csproj -- [PathToFirstXMLFile] [PathToSecondXMLFile]
```

# mac/linux
```sh
dotnet run --project ./XMLcompare/XMLcompare.csproj -- [PathToFirstXMLFile] [PathToSecondXMLFile]
```

# examples
running 
```sh
dotnet run --project ./XMLcompare/XMLcompare.csproj -- "\firstFile.xml" "\secondFile.xml"
```
output
```sql
adding table Users
    adding column ID
    adding column Name
    adding column Email
...
tables in left file only:
    Configuration
...
```

running (when no differences)
```sh
dotnet run --project ./XMLcompare/XMLcompare.csproj -- "\firstFile.xml" "\secondFile.xml"
```
output
```sql
no differences found between the two XML files.
```

errors
```sh
dotnet run --project ./XMLcompare/XMLcompare.csproj -- "\nonexistentFile.xml" "\secondFile.xml"
```
output
```sql
error: could not find file '\nonexistentFile.xml'.
```
