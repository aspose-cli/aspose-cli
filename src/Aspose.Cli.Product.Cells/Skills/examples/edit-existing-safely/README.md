# Example: edit an existing workbook safely

The protocol for a file the user hands you and cares about: one backup
copy before the first write, in-place edits, read-back, a review, and a
report built from a real diff. Input: [update-ops.json](update-ops.json).
Run from this directory.

```powershell
# 1. Fabricate the "user's" workbook so this replays anywhere (a real
#    session starts here, with a file you did not create).
aspose-cli cells create quarterly.xlsx --sheets Q3 --overwrite --output json
aspose-cli cells edit quarterly.xlsx --in-place --set "Q3!A1=Units" --set "Q3!A2=Price" --set "Q3!A3=Rebate" --set "Q3!A4=Revenue" --set "Q3!B1=1180" --set "Q3!B2=42.5" --set "Q3!B3=250" --set "Q3!B4==B1*B2-B3" --output json

# 2. Requested edits with native backup and verification.
aspose-cli cells edit quarterly.xlsx --ops update-ops.json --in-place --backup --verify --output json
aspose-cli cells edit quarterly.xlsx --set "Q3!B3=500" --in-place --verify --output json

# 3. Read the changed range back; dependent formulas recalculated.
aspose-cli cells query range quarterly.xlsx --range Q3!A1:B4 --scope formulas --output json
#    -> B1 1240, B2 44.5, B3 500; B4 v: 54680, f: "=B1*B2-B3"

# 4. Review the result and open every sheet image it lists.
aspose-cli review quarterly.xlsx --out quarterly.review --output json

# 5. The session's exact change inventory: diff against the backup.
aspose-cli cells compare quarterly.backup.xlsx quarterly.xlsx --output json
#    -> summary.cellsDiffering: 4 (B1, B2, B3, recalculated B4 49900 -> 54680)
#    Evaluation saves may also add warning sheets; report those separately.
```

Report from the diff, not from memory: each changed cell with old and
new values, plus the backup path (quarterly.backup.xlsx). In evaluation
mode the edited file also carries the Aspose watermark — tell your user.
