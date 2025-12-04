Konsolowe narzędzie (CLI) napisane w platformie .NET, służące do zarządzania schematem bazy danych Firebird 5.0

Aplikacja oferuje trzy główne tryby pracy:
1. Build (build-db): Tworzenie nowej, czystej bazy danych i automatyczne wykonanie wszystkich skryptów SQL. 

2. Update (update-db): Aktualizacja istniejącej bazy danych.

3. Export (export-scripts): Generowanie skryptów SQL na podstawie istniejącej bazy danych (Domeny, Tabele, Procedury).

Narzędzie przystosowane jest do pracy z plikamy o rozszerzeniu `.sql`.
Aby zbudować projekt należy użyć polecenia: dotnet build -c Release

Uwaga:
Podczas budowania bazy danych (build-db), connection string jest tworzony na podstawie przekazanej ścieżki folderu, w związku z czym w celu np. zmiany serwera bazy lub użycia innego użytkownika bazodanowego należy zmodyfikować owy connection string w metodzie BuildDatabase w pliku Program.cs.