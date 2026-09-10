# Example: change assumptions, recalculate, verify

The pattern that makes delivered numbers trustworthy: never do the math
yourself — write inputs, let the real engine recalculate, read results
back. Run from this directory.

```sh
# 1. Build a tiny model: revenue, growth assumption, projection formula.
aspose-cli cells create model.xlsx --sheets Model --overwrite
aspose-cli cells edit model.xlsx --ops model-ops.json --in-place --output json

# 2. Read the projected value the engine computed.
aspose-cli cells query range model.xlsx --range B3 --scope formulas --output json
#    -> v: 110000.00000000001, f: "=B1*(1+B2)"
#    (IEEE 754 noise from 0.1, exactly as in Excel itself — round for
#     display with a numberFormat, never by rewriting the value.)

# 3. Change the assumption; dependent formulas recalculate automatically.
aspose-cli cells edit model.xlsx --ops '{"ops":[{"op":"set_values","range":"B2","values":[[0.25]]},{"op":"recalculate"}]}' --in-place

# 4. Verify: the projection now reflects the new growth rate.
aspose-cli cells query range model.xlsx --range B1:B3 --output json
#    -> B3 v: 125000

# 5. Look at it the way a human would.
aspose-cli cells render model.xlsx --range A1:B3 --out model.png --overwrite
```

Deliver values from step 4's `query range` output — they came from the engine's
calculation chain, not from arithmetic in your head.
