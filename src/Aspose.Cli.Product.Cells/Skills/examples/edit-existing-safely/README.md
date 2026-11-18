# Example: edit an existing workbook safely

The protocol for a file the user hands you and cares about: in-place
edits, read-back and a render. Input: [update-ops.json](update-ops.json).
Run from this directory.

```powershell
# 1. Fabricate the "user's" workbook so this replays anywhere (a real
#    session starts here, with a file you did not create).
aspose-cli cells create quarterly.xlsx --sheet-names Q3 --overwrite --output-format json
aspose-cli cells edit quarterly.xlsx --in-place --set "Q3!A1=Units" --set "Q3!A2=Price" --set "Q3!A3=Rebate" --set "Q3!A4=Revenue" --set "Q3!B1=1180" --set "Q3!B2=42.5" --set "Q3!B3=250" --set "Q3!B4==B1*B2-B3" --output-format json

# 2. Requested edits with verification.
aspose-cli cells edit quarterly.xlsx --ops update-ops.json --in-place --verify --output-format json
aspose-cli cells edit quarterly.xlsx --set "Q3!B3=500" --in-place --verify --output-format json

# 3. Read the changed range back; dependent formulas recalculated.
aspose-cli cells read quarterly.xlsx --range Q3!A1:B4 --content formulas --output-format json
#    -> B1 1240, B2 44.5, B3 500; B4 v: 54680, f: "=B1*B2-B3"

# 4. Render the result and look at the rendered images.
aspose-cli cells render quarterly.xlsx --sheets all --out quarterly.png --output-format json
```

In evaluation mode the edited file also carries the Aspose watermark — tell
your user.
