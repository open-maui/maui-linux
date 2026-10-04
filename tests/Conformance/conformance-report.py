#!/usr/bin/env python3
"""Summarizes a conformance TRX run per handler test class.

Usage: conformance-report.py <results.trx> [--failures] [--class NAME]

Prints a markdown table (ported / passed / failed / skipped per test class)
and, with --failures, every failing test with the first line of its message.
The per-failure diagnoses in docs/CONFORMANCE.md are written by hand on top
of this output.
"""
import sys
import xml.etree.ElementTree as ET
from collections import OrderedDict, defaultdict

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def load(path):
    root = ET.parse(path).getroot()
    defs = {}
    for d in root.iterfind(".//t:UnitTest", NS):
        m = d.find("t:TestMethod", NS)
        cls = m.get("className").split(".")[-1]
        defs[d.get("id")] = (cls, m.get("name"))
    rows = []
    for r in root.iterfind(".//t:UnitTestResult", NS):
        cls, method = defs.get(r.get("testId"), ("?", "?"))
        msg_el = r.find("t:Output/t:ErrorInfo/t:Message", NS)
        msg = (msg_el.text or "").strip() if msg_el is not None else ""
        stdout = r.find("t:Output/t:StdOut", NS)
        rows.append({
            "class": cls,
            "method": method,
            "name": r.get("testName"),
            "outcome": r.get("outcome"),
            "message": msg,
            "skip": (stdout.text or "").strip() if stdout is not None else "",
        })
    return rows


def main():
    args = sys.argv[1:]
    path = args[0]
    show_failures = "--failures" in args
    only = args[args.index("--class") + 1] if "--class" in args else None
    rows = load(path)
    per = OrderedDict()
    for r in sorted(rows, key=lambda r: r["class"]):
        c = per.setdefault(r["class"], defaultdict(int))
        c["total"] += 1
        c[r["outcome"]] += 1
    print("| Test class | Ported | Passed | Failed | Skipped |")
    print("|---|---:|---:|---:|---:|")
    tot = defaultdict(int)
    for cls, c in per.items():
        skipped = c["NotExecuted"]
        print(f"| {cls} | {c['total']} | {c['Passed']} | {c['Failed']} | {skipped} |")
        for k in ("total", "Passed", "Failed", "NotExecuted"):
            tot[k] += c[k]
    print(f"| **Total** | **{tot['total']}** | **{tot['Passed']}** | **{tot['Failed']}** | **{tot['NotExecuted']}** |")
    if show_failures:
        for r in sorted(rows, key=lambda r: (r["class"], r["name"])):
            if r["outcome"] != "Failed" or (only and r["class"] != only):
                continue
            first = " ".join(r["message"].split())[:400]
            print(f"\n[{r['class']}] {r['name']}\n    {first}")


if __name__ == "__main__":
    main()
