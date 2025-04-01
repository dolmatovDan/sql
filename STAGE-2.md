# Создание и удаление таблиц

На втором этапе реализуем:
1) Логику создания и удаления таблиц
2) Начнем писать парсер SQL запросов
3) Добавим в проект ручки (endpoint'ы) для исполнения SQL запросов и несколько дополнительных для получения списка таблиц и схемы таблиц

## SQL запросы которые будем учиться парсить и исполнять в этой части работы

В этой части работы поддержим две команды: создание и удаление таблиц. В SQL виде они выглядят так:

```sql
-- Создание таблицы
CREATE TABLE [IF NOT EXISTS] table_name (
    column1_name TYPE [PRIMARY KEY] [NOT NULL] [DEFAULT value],
    column2_name TYPE [NOT NULL] [DEFAULT value],
    ...
    columnN_name TYPE [NOT NULL] [DEFAULT value],
);

-- Удаление таблицы
DROP TABLE [IF EXISTS] table_name;
```
Немного расшифрую написанное:
1) ';' в конце запроса ставится обычно только в скриптах в которых может быть много последовательных SQL команд, однако т.к. мы делаем ручку которая будет исполнять только одину команду за раз, то ';' в конце - должна просто игнорироваться вашим парсером
2) ключевые слова SQL записаны большими буквами, имена/значения маленькими, однако ключевые слова SQL регистронезависимы, потому если переписать все маленькими буквами, то все должно парсится корректно, при этом учтите, что данные, например value у ключевого слова DEFAULT являются чувствительными к регистру учитывайте это при реализации парсера
3) ключевые слова взятые в [] являются необязательной частью SQL выражения, но тут как всегда есть куча частных случаев
4) IF NOT EXISTS - для CREATE TABLE, если присутствует, то ручка всегда должна возвращать ответ OK(200) и результатом того что таблица была создана или нет, если отсутствует, то в случае попытки создать таблицу с именем уже существующей, необходимо вернуть из ручки Conflict(409) 
5) PRIMARY KEY - признак того, что данное поле является ключом, это значит, что оно должно иметь уникальное значение для каждой записи в таблице, не может быть NULL, т.е. NOT NULL - применяется к данной колонке автоматически и его можно не писать
6) NOT NULL - признак того, что значения в данной колонке не могут быть NULL'ами, т.е. при добавлении данных (эту команду мы будем реализовывать в будущем) всегда требуется указывать значение конкретное значение, которое не NULL
7) DEFAULT - это ключевое слово, которое задает значение данного поля по умолчанию, используется при добавлении в таблицу новых записей (строк), если при добавлении оно не указано, то в таблицу необходимо записать именно это значение по умолчанию
8) IF EXISTS - для DROP TABLE, если присутствует, то ручка всегда должна возвражать OK(200) и результат true/false в зависимости от наличия таблицы, если не указан, тогда ручка должна вернуть ошибку NotFound(404)

Пара слов о том, какие типы мы будем поддерживать в нашей базе, ниже приведена таблица с пояснением:
```
SQL тип     C# тип      Комментарий
-----------------------------------
BOOLEAN     Boolean     В SQL есть два ключевых слова TRUE и FALSE для задания значений
INTEGER     Int64       Знаковое 64-х битное число
FLOAT       Double      64-х битное с плавающей точкой
STRING      String      Unicode-строка (кодировка: utf16-le)
SERIAL      Int64       Счетчик, который используется в качестве
                        PRIMARY KEY для идентификации записей в таблице.
                        Каждый раз при добавлении новой записи в таблицу инкрементируется.
                        Для хранения текущего значения счетчика обычно СУБД создает отдельную таблицу
                        в которой одна колонка с типом INTEGER и одна запись с текущим значением
                        счетчика. Давайте договоримся, что в нашем SQL диалекте будет возмжно
                        использовать данный тип только совместно с PRIMARY KEY. Для хранения значения
                        думаю можно поступить как и другие базы данных.
``` 
Теперь приведу несколько примеров корректных запросов на создание таблицы:
```sql
CREATE TABLE tab1 (id SERIAL PRIMARY KEY);
create table tab2 (name string not null, description string);
CREATE TABLE tab3 (
    "id" String Primary Key,
    column BOOLEAN DEFAULT TRUE
);
```
Как видно SQL запрос так же может быть разбит на строки, а может быть записан в одну строку, грубо говоря, переводы строк можно интерпретировать как пробелы.
А теперь несколько примеров некорректных запросов (парсер должен корректно обрабатывать все такие случаи, приведенные здесь и не только):
```sql
-- Согласно нашей договоренности id - должен быть помечен как PRIMARY KEY
CREATE TABLE table (id SERIAL);

-- Может быть только один PRIMARY KEY в таблице
CREATE TABLE table (
    id1 STRING PRIMARY KEY,
    id2 INTEGER PRIMARY KEY
);

-- Неизвестный тип
CREATE TABLE table (id BIGINT);

-- Некорректный тип значения по умолчанию (уже присутствует пометка о том, что не может быть NULL)
CREATE TABLE table (name STRING NOT NULL DEFAULT NULL);

-- Некорректный тип значения по умолчанию (не соответсвуют типы)
-- (обратите внимание на то, что значения строк берутся в одинарные кавычки, а не в двойные)
CREATE TABLE table (name INTEGER NOT NULL DEFAULT 'Hello world!');
```

## Новые ручки (endpoint'ы)

### GET /api/v1/tables/list
```
GET /api/v1/tables/list

input: нет входных аргументов

output:
{
  tables: ["table1", "table2", ..., "tableN"]
}
```
Данная ручка должна возвращать список имен таблиц, которые в данный момент созданы в
нашей базе. Чтобы получить такой JSON на выходе можно в качестве результат использовать
класс вида:
```csharp
public class GetTablesOutput
{
    [Required] public String[] Tables { get; set; }
}
```

### POST /api/v1/tables/schema
```
POST /api/v1/tables/schema

input:
{
  "name": "my_table_name"
}

output:
{
  "schema": {
    "columns": [                // список информации по колонкам (в порядке их добавления при создании таблицы)
      {
        "name": "id",           // имя колонки
        "type": "serial",       // тип данныз в колонке
        "isPKey": true,         // признак того, что это PRIMARY KEY
        "isNullable": false,    // признак того, что поле зануляемое
        "defaultValue": {       // информация о значении по умолчанию
          "isSpecified": false, // признак того, что DEFAUL был задан для колонки
          "isNull": false,      // признак того, что значение по умолчанию = NULL
          "value": ""           // значение по умолчанию в строковом виде
        }
      },
      {
        "name": "description",
        "type": "string",
        "isPKey": false,
        "isNullable": true,
        "defaultValue": {
          "isSpecified": true,
          "isNull": true,
          "value": ""
        }
      },
      ...                       // и так далее
    ]
  }
}
```
В виде классов со стороны сервиса подойдет такое представление:
```csharp
public class DefaultValueInfo
{
    [Required] public Boolean IsSpecified { get; set; }
    [Required] public Boolean IsNull { get; set; }
    [Required] public String Value { get; set; }
}

public class TableSchemaColumnInfo
{
    [Required] public String Name { get; set; }
    [Required] public String Type { get; set; }
    [Required] public Boolean IsPKey { get; set; }
    [Required] public Boolean IsNullable { get; set; }
    [Required] public DefaultValueInfo DefaultValue { get; set; }
}

public class TableSchemaInfo
{
    [Required] public TableSchemaColumnInfo[] Columns { get; set; }
}

public class PostTablesSchemaOutput
{
    [Required] public TableSchemaInfo Schema { get; set; }
}
```
Ручка должна по имени таблицы возвращать схему таблицы, которая представляет из себя набор
информации о таблице и колонках. Давайте рассмотрим пример запроса которым создается таблица
и результата получения схемы:
```sql
CREATE TABLE goods (
    "id" SERIAL PRIMARY KEY,
    "name" STRING NOT NULL,
    "description" STRING DEFAULT NULL,
    "price" INTEGER NOT NULL DEFAULT 10,
    "stock" INTEGER,
    "is_foreign" BOOLEAN NOT NULL
)
```
Обратите внимание на то, что в SQL что-то взятое в двойные кавычки - это имя (например колонки или таблицы). Если же вам
нужно задать значение для строки, то следует использовать одинарные кавычки. Двойные кавычки обычно используются для
разрешения конфликта имен с ключевыми словами. Например слово user может быть зарезервировано СУБД под переменную хранящую текущее имя пользователя от
которого выполняется запрос. Однако если его взять в двойные кавычки "user" - то интерпретатор запроса однозначно определит
его как имя колонки или таблицы, или какого-то другого объекта, но не как ключевое слово языка. А так в целом, двойные кавычки можно не ставить.
При этом поддержать эту особенность синтаксиса, при разборе запросов - нужно.
Для созданной таким образом таблицы, мы должны получить такой результат при запросе ее схемы:
```json
{
  "schema": {
    "columns": [
      {
        "name": "id",
        "type": "serial",
        "isPKey": true,
        "isNullable": false,
        "defaultValue": {
          "isSpecified": false,
          "isNull": false,
          "value": ""
        }
      },
      {
        "name": "name",
        "type": "string",
        "isPKey": false,
        "isNullable": false,
        "defaultValue": {
          "isSpecified": false,
          "isNull": false,
          "value": ""
        }
      },
      {
        "name": "description",
        "type": "string",
        "isPKey": false,
        "isNullable": true,
        "defaultValue": {
          "isSpecified": true,
          "isNull": true,
          "value": ""
        }
      },
      {
        "name": "price",
        "type": "integer",
        "isPKey": false,
        "isNullable": false,
        "defaultValue": {
          "isSpecified": true,
          "isNull": false,
          "value": "10"
        }
      },
      {
        "name": "stock",
        "type": "integer",
        "isPKey": false,
        "isNullable": true,
        "defaultValue": {
          "isSpecified": false,
          "isNull": false,
          "value": ""
        }
      },
      {
        "name": "is_foreign",
        "type": "boolean",
        "isPKey": false,
        "isNullable": false,
        "defaultValue": {
          "isSpecified": false,
          "isNull": false,
          "value": ""
        }
      }
    ]
  }
}
```

### POST /api/v1/query

```
POST /api/v1/query

input:
{
  "query": "SELECT id, name FROM table"
}

output:
{
  "schema": {
    "columns": [
      {
        "name": "id",
        "type": "serial",
        "isPKey": true,
        "isNullable": false,
        "defaultValue": {
          "isSpecified": false,
          "isNull": false,
          "value": ""
        }
      },
      {
        "name": "name",
        "type": "string",
        "isPKey": false,
        "isNullable": false,
        "defaultValue": {
          "isSpecified": false,
          "isNull": false,
          "value": ""
        }
      }
    ]
  },
  "result": [
    ["1", "record1"],
    ["2", null],
    ["3", "some name"]
  ]
}
```
Здесь приведен общий вид ответа на произвольные SQL запросы. Фактически в ответе необходимо
выдать схему таблицы которую мы получили при выполнении запроса и строки с данными.
Схема формируется теми же структурами, что и в предыдущей ручку. А массив "result"
содержит строки, каждое из значений которого соответсвует схеме. Обратите внимание
в результате все значения отдаются строками - это нужно чтобы упростить формирование ответа
на стороне сервиса. Так же зануляемые типы могут содержать null - это тоже будет работать из
коробки, достаточно просто присвоить null соответсвующей строке при возврате результата.

В текущей части работы у нас не будет таких "больших" и "сложных" таблиц в результате, т.к.
мы реализуем только создание и удаление таблиц. Операции CREATE TABLE и DROP TABLE
должны возвращать таблицу состоящую из единственной колонки "result" и единственной строки
в которой будет результат выполнения команды true - в случае успешного создания/удаления таблицы
false - в обратном случае.

Вот так должен выглядеть ответ для этих запросов. Сразу делайте обобщенный код для возврата
значения для произвольной таблицы.
```json
{
  "schema": {
    "columns": [
      {
        "name": "result",
        "type": "boolean",
        "isPKey": false,
        "isNullable": false,
        "defaultValue": {
          "isSpecified": false,
          "isNull": false,
          "value": ""
        }
      }
    ]
  },
  "result": [
    ["true"]
  ]
}
```
## Задание

Итак подведем черту, что необходимо сделать:
1) Проработать классы Table и TableSchema, которые будут хранить данные таблицы и схему данных таблицы соответсвенно.
2) Реализовать парсер SQL команд CREATE TABLE и DROP TABLE, который превращает запросы в исполняемые команды. Возможный вариант организации интерфеса команд и пример мы рассматривали на практике.
3) Реализовать 3 новые ручки с логикой работы как описано в задании.

