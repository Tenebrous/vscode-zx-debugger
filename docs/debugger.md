Interfaces to emulators/machines is performed by inheriting the Debugger class.

This provides the following items to be overridden.



## public virtual Meta Meta { get; }

Return a new Meta containing capabilities.


## public virtual void RefreshRegisters( Registers pRegisters )

Retrieve the registers from the emulator/machine, and update pRegisters.


## public virtual void SetRegister( Registers pRegisters, string pRegister, ushort pValue )

Set the specified register to the value provided.

## public virtual void RefreshMemoryPages( Memory pMemory )

Update the memory page layout - the state is recorded by using pMemory.SetAddressBank:

```csharp
pMemory.SetAddressBank( 
    (ushort)( 0x0000 ),               // start of slot
    (ushort)( 0x3FFF ),               // end of slot
    pMemory.Bank( BankID.ROM( 0 ) )   // memory bank in that slot 
);
```

Additionally, whether paging is enabled or disabled is recorded using pMemory.PagingEnabled:
```csharp
pMemory.PagingEnabled = true;
```

# Classes

## Meta

```csharp
/// <summary>
/// The debugger can change the value of registers
/// </summary>
public bool CanSetRegisters;

/// <summary>
/// The debugger can evaluate an arbitrary string and return a result
/// </summary>
public bool CanEvaluate;

/// <summary>
/// The debugger supports stepping out
/// </summary>
public bool CanStepOut;

/// <summary>
/// The debugger will ignore step over for 'ret' and 'jp' and will instead do a normal step
/// </summary>
public bool CanStepOverSensibly;

/// <summary>
/// Maximum number of breakpoints enabled at any one time
/// </summary>
public int MaxBreakpoints;
```

## Registers

```csharp
public byte   A;
public byte   F;
public byte   B;
public byte   C;
public byte   D;
public byte   E;
public byte   H;
public byte   L;

public byte   AltA;
public byte   AltF;
public byte   AltB;
public byte   AltC;
public byte   AltD;
public byte   AltE;
public byte   AltH;
public byte   AltL;

public byte   IXH;
public byte   IXL;
public byte   IYH;
public byte   IYL;

public ushort PC;
public ushort SP;

public byte   I;
public byte   R;
```

## BankID

```
public struct BankID
{
    public readonly TypeEnum Type;  // All, ROM, Bank, DIV
    public readonly int Number;
}
```

---

### public BankID( TypeEnum pType, int pNumber = 0 )

---

### public BankID( string pBank )
Create a BankID by parsing the provided string into a bank type & number

---

### public BankID( string pType, int pNumber )
Create a BankID from the textual bank type & number

---

### public static BankID ROM( int pID )
Create a BankID for the specified ROM number

---

### public static BankID Bank( int pID )
Create a BankID for the specified BANK number

---

### public static BankID Unpaged()
Create a BankID for unpaged memory


## Bank

```
public class Bank
{
    public BankID ID;
    public bool   IsPagedIn;
    public ushort LastAddress;
    public string Name => ID.ToString();
}
```

## Memory

```
n/a
```

---

### public Bank Bank( BankID pID )

Get the specified bank.

---

### public void SetAddressBank( ushort pMin, ushort pMax, Bank pBank )

Set the memory slot from pMin to pMax to be associated with bank pBank.

