# Yuzu binary format

  * [Description](#description)
  * [Details](#details)

## Description

Yuzu binary format is optimized for fast serialization and deserialization,
at the slight expense of density.
If maximal density is required, it is recommended to additionaly compress serialized data.

Although format is not strictly tied to C#/.NET, some format features correspond directly
to C#/.NET ones and may require some extra conversion on other platforms.

Yuzu binary format and Yuzu library provides extensive support for data migration.
In particular, roundtrip is guaranteed even in the presence of unknown fields and unknown types
(i.e. older version of code may read, modify and write data created by newer version,
preserving new fields unknown to older version).

Yuzu serialization and deserialization is (mostly) done in a single pass, both on object graph and serialized data stream.
Exceptions (such as `CheckForEmptyCollections` option) are mentioned explicitly in the [reference](reference.md).

Yuzu binary metadata is intermingled with data, with each new structured type described on the first occurrence.
This avoids extra object graph pass to gather metadata, but creates some limits for reordering of serialized stream.

Yuzu binary data stream may be split at top-level object boundaries.

Individual sub-trees of object graph may be reordered between serialization and deserialization as long as
they do not introduce any new metadata.

## Details

Yuzu binary consists of optional signature followed by a serialized data item.
Item contains type header and data.
Type header starts with a single-byte rough type:

Value | Description
---:| ---
  1 | `sbyte`
  2 | `byte`
  3 | `short`
  4 | `ushort`
  5 | `int`
  6 | `uint`
  7 | `long`
  8 | `ulong`
  9 | `bool`
 10 | `char`
 11 | `float`
 12 | `double`
 13 | `decimal`
 14 | `DateTime`
 15 | `TimeSpan`
 16 | `string`
 17 | *Any* (`object`)
 18 | `Nullable`
 19 | `DateTimeOffset`
 20 | `Guid`
 32 | *Record*
 33 | *Sequence* (list, enumarable or array with rank = 1)
 34 | *Mapping* (dictionary)
 35 | *NDimArray* (array with rank > 1)

Basic types are immediately followed by value, where integers are stored in little-endian order,
floating point values in IEEE 754 representation, char as UTF-8.

String is serialized as a varint length followed by a sequence of UTF-8 bytes.
If the length is zero, it is followed by either a zero byte indicating empty string or a byte with value 1 indicating `null`.

`Nullable` is followed by item type, then a zero byte to represent `null` or a byte with value 1 followed by item value.

*Sequence* (denoting 1-dimensional arrays and collections) is followed by item type, then by 4-byte item count and item representations.
Null sequence is designated by item count −1 (`FF FF FF FF`).

*Mapping* (denoting dictionaries) is followed by key type, then by item type, then by 4-byte entry count and entry representations.
Each entry consists of key followed by value.
Null mapping is designated by item count −1 (`FF FF FF FF`).

*Record* denotes structured types (`class` and `struct`). It is followed by:
1. 2-byte type index. Indexes are counting from 1 upwards without gaps in order of type appearance in serialized stream.
2. If this index is new (i.e. type did not yet occur in current stream), 2-byte number of fields, followed by field descriptions.
3. For each field:
  1. 2-byte field index starting with 1 with possible gaps (so some fields may be omitted) 
  2. If field type was Any (17), type of specific value.
  3. Value representation.
4. 2 zero bytes.

Field description is:
1. 2-byte field index. Indexes are counting from 1 upwards without gaps
2. Field name length, varint-encoded.
3. Field name in UTF-8.
4. Field type.

*NDimArray* denotes an array with 2 or more dimensions. It is followed by:
1. 1-byte rank (number of dimensions)
2. For each dimension, 4-byte length.
3. 1-byte equal to 1 if array has at least one non-zero lower bound, 0 otherwise.
4. If previous item is 1, for each dimension, 4-byte lower bound.
5. Item representations in last-index-fastest order.

Null array is designated by value −1 (`FF FF FF FF`) immediately after the rank.
