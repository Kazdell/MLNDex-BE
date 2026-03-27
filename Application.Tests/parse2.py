import xml.etree.ElementTree as ET
import glob
import os

trx_files = glob.glob('c:/Users/ACER/Downloads/MLNDex/MLNDex-BE/Application.Tests/TestResults/*.trx')
if not trx_files:
    print("No TRX files found.")
    exit(0)

latest_trx = max(trx_files, key=os.path.getctime)
print(f"Parsing {latest_trx}...\n")

tree = ET.parse(latest_trx)
root = tree.getroot()
ns = {'ns': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}

results = root.findall('.//ns:UnitTestResult[@outcome="Failed"]', ns)
for result in results:
    test_name = result.get('testName')
    error_message = result.find('.//ns:Output/ns:ErrorInfo/ns:Message', ns)
    trace = result.find('.//ns:Output/ns:ErrorInfo/ns:StackTrace', ns)
    print(f"FAIL: {test_name}")
    if error_message is not None:
        print(error_message.text.strip())
    if trace is not None:
        print(trace.text.strip()[:600])
    print("-" * 50)
print(f"Total failures: {len(results)}")
