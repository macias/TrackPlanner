the only difference between two implementations is selection of unusedValue
* Fill -- it is max int, so all values are stored as-is, but we need to fill/set initial array
* Shift -- it is zero, so all values have to be shifted, but new array is ready for use without prefill

similarly to original Dictionary we keep an array of indices, initially key+value is placed as their hash
position -- this is in-sync position, and we indicate it with keeping last bit 0 (so the number is positive)

if we add another key with the same hash, we place it somewhere else, so we puth the index from hash to that
placement and from the other placement back to hash. However back-index is at non-hash position thus we mark
it with last bit set = 1

#### EXAMPLE:
adding key A with local hash = 2
```
indices   keys
[not used] [ ]
[not used] [ ]
[2]  -->   [A]
```
let's add B with the same hash, we cannot add at the same spot, so we find first not-occupied, and then set the indices
```
[2 | FLAG] [B]
[not used] [ ]
[0]        [A]
```        
please note first row has the out-of-sync set because it is not "its" slot, while the last one does not have such flag
the last entry (in-sync) will never be rellocated. Also, once starting from the hash position, you can loop around
and iterate over all entries with the same hash (here hash: 2 -> 0 -> 2)

ok, let's add C with hash 0, this slot is occupied by out-of sync-entry so first we have to move it
```
[not used] [ ]
[2 | FLAG] [B]
[1]        [A]
```
and then add new entry
```
[0]        [C]
[2 | FLAG] [B]
[1]        [A]
```


* https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs
* https://blog.markvincze.com/back-to-basics-dictionary-part-2-net-implementation/
* https://docs.microsoft.com/en-us/previous-versions/ms379570(v=vs.80)
* https://docs.microsoft.com/en-us/previous-versions/ms379571(v=vs.80)
* https://docs.microsoft.com/en-us/previous-versions/ms379572(v=vs.80)
* https://docs.microsoft.com/en-us/previous-versions/ms379573(v=vs.80)
* https://docs.microsoft.com/en-us/previous-versions/ms379574(v=vs.80)
* https://docs.microsoft.com/en-us/previous-versions/ms379575(v=vs.80)
* https://stackoverflow.com/questions/1100311/what-is-the-ideal-growth-rate-for-a-dynamically-allocated-array
* https://stackoverflow.com/questions/24831998/lists-double-their-space-in-c-sharp-when-they-need-more-room-at-some-point-does
     



