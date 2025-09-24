# StringsToSection

## 📜 What is StringsToSection ?

StringsToSection is a **string relocation & hiding tool** for .NET assemblies.

It packs string literals into a custom PE section, compresses them, and replaces `ldstr` with indexed lookups that are resolved by a runtime loader. The technique is inspired by the Anti-Tamper system from ConfuserEx, but applied to string literals rather than method bodies.

---

## ⚠️ Disclaimer

StringsToSection is **experimental** and **not production-ready**.

- It can **break** certain programs (auto-generated resource handlers, reflection-based lookups, etc.).  
- You must **test thoroughly** before using on real-world binaries.  
- Use on your own code or with permission only.
- Be careful with formatted strings passed to `String.Format` or resource lookups.
- Combining with other protections that move or encrypt metadata can break the loader.

---

## 🎯 Key Features

- **PE section packing**: Strings moved to a PE section.  
- **Per-string compression**: Each string is GZip-compressed.  
- **Alignment-safe storage**: Blocks padded to 4 bytes for consistent parsing.  
- **Runtime loader**: Unsafe, minimal loader reconstructs strings at module initialization.  
- **Index replacement**: `ldstr` → `<Module>.s[index]` (or equivalent) at IL level.  
- **Exclusions**: Critical strings (resource identifiers, property names) can be skipped.

---

## 🔍 Example

**Original:**
```csharp
Console.WriteLine("Hello, world!");
```

**Protected:**
```csharp
Console.WriteLine(<Module>.s[0]);
```

---

## 🧪 Testing checklist (before trusting output)

* Run protected binary on **x86** and **x64** (if you support both).
* Verify UI apps start and resources (icons, images, localized strings) show correctly.
* Run with a debugger and step through module initializer to ensure loader reads the section without errors.
* Check edge cases: reflection lookups, assembly loading, and native interop that expects literal names.
* Test on real-world large assemblies to confirm no corrupted blocks or invalid lengths.

---

## 📢 Credits

* [ConfuserEx](https://github.com/yck1509/ConfuserEx)
* [dnlib](https://github.com/0xd4d/dnlib)