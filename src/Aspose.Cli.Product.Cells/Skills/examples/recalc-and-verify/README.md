# Example: change assumptions, recalculate, verify

The pattern that makes delivered numbers trustworthy: never do the math
yourself — write inputs, let the real engine recalculate, read results
back. Run from this directory.

```powershell
# 1. Build a tiny model: revenue, growth assumption, projection formula.
aspose-cli cells create model.xlsx --sheets Model --overwrite --output json
aspose-cli cells edit model.xlsx --ops model-ops.json --in-place --output json

# 2. Read the projected value the engine computed.
aspose-cli cells query range model.xlsx --sheet Model --range B3 --scope formulas --output json
#    -> v: 110000.00000000001, f: "=B1*(1+B2)"
#    (IEEE 754 noise from 0.1, exactly as in Excel itself — round for
#     display with a numberFormat, never by rewriting the value.)

# 3. Change the assumption; dependent formulas recalculate automatically.
aspose-cli cells edit model.xlsx --set "Model!B2=0.25" --in-place --output json

# 4. Verify: the projection now reflects the new growth rate.
aspose-cli cells query range model.xlsx --sheet Model --range B1:B3 --output json
#    -> B3 v: 125000

# 5. Look at it the way a human would.
aspose-cli review model.xlsx --out model.review --output json
```

Deliver values from step 4's `query range` output — they came from the engine's
calculation chain, not from arithmetic in your head.
