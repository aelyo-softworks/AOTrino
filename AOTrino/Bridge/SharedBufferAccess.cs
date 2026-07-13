namespace AOTrino.Bridge;

// who may write the shared buffer once posted to the page
public enum SharedBufferAccess
{
    // .NET writes, JS reads only
    ReadOnly,

    // both .NET and JS may read and write the same memory
    ReadWrite,
}
