namespace Aspose.Cli.Host.Review;

/// <summary>Orders numbered evidence naturally while retaining ordinal text and tie ordering.</summary>
internal sealed class ReviewArtifactPathComparer : IComparer<string>
{
    public static ReviewArtifactPathComparer Instance { get; } = new();

    private ReviewArtifactPathComparer() { }

    public int Compare(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return string.CompareOrdinal(left, right);
        }

        int leftIndex = 0;
        int rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (char.IsAsciiDigit(left[leftIndex]) && char.IsAsciiDigit(right[rightIndex]))
            {
                int leftEnd = DigitRunEnd(left, leftIndex);
                int rightEnd = DigitRunEnd(right, rightIndex);
                while (leftIndex < leftEnd && left[leftIndex] == '0')
                {
                    leftIndex++;
                }
                while (rightIndex < rightEnd && right[rightIndex] == '0')
                {
                    rightIndex++;
                }

                // Compare arbitrary-length integers without parsing or constructing padded keys.
                int order = (leftEnd - leftIndex).CompareTo(rightEnd - rightIndex);
                if (order == 0)
                {
                    order = left.AsSpan(leftIndex, leftEnd - leftIndex)
                        .SequenceCompareTo(right.AsSpan(rightIndex, rightEnd - rightIndex));
                }
                if (order != 0)
                {
                    return order;
                }
                leftIndex = leftEnd;
                rightIndex = rightEnd;
            }
            else
            {
                int order = left[leftIndex++].CompareTo(right[rightIndex++]);
                if (order != 0)
                {
                    return order;
                }
            }
        }

        int remainder = (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
        // Leading zeros only break a tie after every logical path segment has been compared.
        return remainder != 0 ? remainder : string.CompareOrdinal(left, right);
    }

    private static int DigitRunEnd(string text, int index)
    {
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }
        return index;
    }
}
