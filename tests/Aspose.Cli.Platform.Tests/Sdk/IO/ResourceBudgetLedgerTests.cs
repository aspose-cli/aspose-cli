using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class ResourceBudgetLedgerTests
{
    [Theory]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void FileAdmission_CoversLimitMinusOneExactAndPlusOne(
        int size,
        bool accepted)
    {
        string path = TemporaryFile(new byte[size]);
        try
        {
            using OperationDeadline deadline = OperationDeadline.Start(null);
            ResourceBudgetLedger budgets = Create(
                deadline,
                (ResourceBudgetKinds.InputBytes, 8),
                (ResourceBudgetKinds.MemoryBufferBytes, 8));

            if (accepted)
            {
                budgets.AdmitFile(path);
                Assert.Equal(size, budgets.Inputs.ReadAllBytes(path).Length);
            }
            else
            {
                CliException error = Assert.Throws<CliException>(
                    () => budgets.AdmitFile(path));
                Assert.Equal(ErrorCodes.FileTooLarge, error.Code);
                Assert.Equal(9L, error.Details!["sizeBytes"]!.GetValue<long>());
                Assert.Equal(8L, error.Details["limitBytes"]!.GetValue<long>());
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void StandardInput_IsBoundedWhileStreaming(int size, bool accepted)
    {
        using OperationDeadline deadline = OperationDeadline.Start(null);
        ResourceBudgetLedger budgets = Create(
            deadline,
            (ResourceBudgetKinds.StandardInputBytes, 8),
            (ResourceBudgetKinds.DecodedTextCharacters, 32));
        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(new string('x', size)));

        if (accepted)
        {
            Assert.Equal(
                new string('x', size),
                budgets.Inputs.ReadStandardInputText(input));
        }
        else
        {
            CliException error = Assert.Throws<CliException>(
                () => budgets.Inputs.ReadStandardInputText(input));
            Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
            Assert.Equal(
                ResourceBudgetKinds.StandardInputBytes,
                error.Details!["resource"]!.GetValue<string>());
        }
    }

    [Fact]
    public void TextDecode_EnforcesByteAndCharacterBudgetsIndependently()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("汉字");

        using (OperationDeadline deadline = OperationDeadline.Start(null))
        {
            ResourceBudgetLedger bytes = Create(
                deadline,
                (ResourceBudgetKinds.StandardInputBytes, utf8.Length - 1),
                (ResourceBudgetKinds.DecodedTextCharacters, 2));
            using var input = new MemoryStream(utf8);
            CliException error = Assert.Throws<CliException>(
                () => bytes.Inputs.ReadStandardInputText(input));
            Assert.Equal(
                ResourceBudgetKinds.StandardInputBytes,
                error.Details!["resource"]!.GetValue<string>());
        }

        using (OperationDeadline deadline = OperationDeadline.Start(null))
        {
            ResourceBudgetLedger characters = Create(
                deadline,
                (ResourceBudgetKinds.StandardInputBytes, utf8.Length),
                (ResourceBudgetKinds.DecodedTextCharacters, 1));
            using var input = new MemoryStream(utf8);
            CliException error = Assert.Throws<CliException>(
                () => characters.Inputs.ReadStandardInputText(input));
            Assert.Equal(
                ResourceBudgetKinds.DecodedTextCharacters,
                error.Details!["resource"]!.GetValue<string>());
        }
    }

    [Fact]
    public void FileGrowthAfterAdmission_IsRejectedBeforeRead()
    {
        string path = TemporaryFile([1, 2, 3]);
        try
        {
            using OperationDeadline deadline = OperationDeadline.Start(null);
            ResourceBudgetLedger budgets = Create(
                deadline,
                (ResourceBudgetKinds.InputBytes, 32));
            budgets.AdmitFile(path);
            using (var stream = new FileStream(
                       path,
                       FileMode.Append,
                       FileAccess.Write,
                       FileShare.Read))
            {
                stream.Write([4]);
            }

            CliException error = Assert.Throws<CliException>(
                () => budgets.Inputs.OpenFile(path));
            Assert.Equal(ErrorCodes.InputChanged, error.Code);
            Assert.Equal(3L, error.Details!["expectedBytes"]!.GetValue<long>());
            Assert.Equal(4L, error.Details["observedBytes"]!.GetValue<long>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SafePublication_RefreshesAdmissionForSameInvocationVerification()
    {
        using var temporaryDirectory = new Aspose.Cli.TestKit.TempDirectory();
        string path = temporaryDirectory.File("admitted.bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        try
        {
            using OperationDeadline deadline = OperationDeadline.Start(null);
            ResourceBudgetLedger budgets = Create(
                deadline,
                (ResourceBudgetKinds.InputBytes, 32));
            budgets.AdmitFile(path);

            new SafeFileWriter(budgets).Write(
                path,
                overwrite: true,
                temporary => File.WriteAllBytes(temporary, [4, 5, 6, 7]));
            Assert.Equal(
                [4, 5, 6, 7],
                budgets.Inputs.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OutputBudget_IsEnforcedBeforeAtomicPublication()
    {
        string path = TemporaryFile([1]);
        try
        {
            using OperationDeadline deadline = OperationDeadline.Start(null);
            ResourceBudgetLedger budgets = Create(
                deadline,
                (ResourceBudgetKinds.OutputBytes, 1));

            CliException error = Assert.Throws<CliException>(() =>
                new SafeFileWriter(budgets).Write(
                    path,
                    overwrite: true,
                    temporary => File.WriteAllBytes(temporary, [2, 3])));
            Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
            Assert.Equal(
                ResourceBudgetKinds.OutputBytes,
                error.Details!["resource"]!.GetValue<string>());

            Assert.Equal([1], File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SecretWithoutNewline_StopsAtCharacterLimit()
    {
        using OperationDeadline deadline = OperationDeadline.Start(null);
        ResourceBudgetLedger budgets = Create(
            deadline,
            (ResourceBudgetKinds.SecretCharacters, 4));

        CliException error = Assert.Throws<CliException>(
            () => budgets.Inputs.ReadSecretLine(new StringReader("abcde")));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        Assert.Equal(
            ResourceBudgetKinds.SecretCharacters,
            error.Details!["resource"]!.GetValue<string>());
    }

    [Fact]
    public void ExpiredDeadlineWinsWhenBudgetWouldAlsoBeExceeded()
    {
        using OperationDeadline deadline =
            OperationDeadline.Start(TimeSpan.FromMilliseconds(10));
        ResourceBudgetLedger budgets = Create(
            deadline,
            (ResourceBudgetKinds.StandardInputBytes, 1));
        Thread.Sleep(25);

        CliException error = Assert.Throws<CliException>(() =>
            budgets.Consume(
                ResourceBudgetKinds.StandardInputBytes,
                2,
                "bytes",
                "stdin-read"));

        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
    }

    [Fact]
    public void RejectedConsumption_PreservesCountersButTerminatesTheInvocation()
    {
        using OperationDeadline deadline = OperationDeadline.Start(null);
        ResourceBudgetLedger budgets = Create(
            deadline,
            (ResourceBudgetKinds.MemoryBufferBytes, 10));
        budgets.Consume(
            ResourceBudgetKinds.MemoryBufferBytes,
            0,
            "bytes",
            "zero");
        budgets.Consume(
            ResourceBudgetKinds.MemoryBufferBytes,
            6,
            "bytes",
            "first");

        CliException error = Assert.Throws<CliException>(() => budgets.Consume(
            ResourceBudgetKinds.MemoryBufferBytes,
            5,
            "bytes",
            "rejected"));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        Assert.Equal(4, budgets.Remaining(ResourceBudgetKinds.MemoryBufferBytes));
        Assert.Same(error, Assert.Throws<CliException>(() => budgets.Consume(
            ResourceBudgetKinds.MemoryBufferBytes, 4, "bytes", "final")));
        Assert.Equal(4, budgets.Remaining(ResourceBudgetKinds.MemoryBufferBytes));
    }

    [Fact]
    public void InvalidUtf8ReturnsEncodingError()
    {
        using OperationDeadline deadline = OperationDeadline.Start(null);
        ResourceBudgetLedger budgets = Create(
            deadline,
            (ResourceBudgetKinds.StandardInputBytes, 8),
            (ResourceBudgetKinds.DecodedTextCharacters, 8));
        using var input = new MemoryStream([0xC3, 0x28]);

        CliException error = Assert.Throws<CliException>(
            () => budgets.Inputs.ReadStandardInputText(input));

        Assert.Equal(ErrorCodes.InputEncodingInvalid, error.Code);
    }

    [Fact]
    public void BudgetedMemoryStream_ChargesGrowthBeforeAllocation()
    {
        using OperationDeadline deadline = OperationDeadline.Start(null);
        ResourceBudgetLedger budgets = Create(
            deadline,
            (ResourceBudgetKinds.MemoryBufferBytes, 8));
        using var stream = new BudgetedMemoryStream(
            budgets,
            ResourceBudgetKinds.MemoryBufferBytes,
            "test-buffer");

        stream.Write([1, 2, 3, 4]);
        stream.Position = 0;
        stream.Write([5, 6, 7, 8]);
        stream.Position = stream.Length;
        stream.Write([9, 10, 11, 12]);

        CliException error = Assert.Throws<CliException>(() => stream.WriteByte(13));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        Assert.Equal(8, stream.Length);
        Assert.Equal(8, stream.Capacity);
        Assert.Equal(0, budgets.Remaining(ResourceBudgetKinds.MemoryBufferBytes));
    }

    private static ResourceBudgetLedger Create(
        OperationDeadline deadline,
        params (string Kind, long Limit)[] limits) =>
        new(
            deadline,
            limits.ToDictionary(
                static value => value.Kind,
                static value => value.Limit,
                StringComparer.Ordinal));

    private static string TemporaryFile(byte[] bytes)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"aspose-cli-budget-{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
