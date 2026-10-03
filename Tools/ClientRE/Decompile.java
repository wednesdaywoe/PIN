// Prints Ghidra's decompilation of FirefallClient.exe functions.
// Each argument is a hex address (inside or at the start of a function) or an MSVC class name such as
// apt::ActiveInitiationCommand, which prints every function in that class's vtable.
// Run through decompile.sh, which passes the arguments and reuses the analysed project.
// @category PIN

import java.util.ArrayList;
import java.util.List;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.mem.Memory;

public class Decompile extends GhidraScript {
	@Override
	protected void run() throws Exception {
		DecompInterface decompiler = new DecompInterface();
		decompiler.openProgram(currentProgram);
		for (String arg : getScriptArgs()) {
			for (Address address : resolve(arg)) {
				Function function = getFunctionContaining(address);
				if (function == null) {
					function = createFunction(address, null);
				}
				if (function == null) {
					println("== " + arg + ": no function at " + address);
					continue;
				}
				DecompileResults results = decompiler.decompileFunction(function, 120, monitor);
				println("== " + arg + " -> " + function.getName() + " @ " + function.getEntryPoint());
				println(results.decompileCompleted() ? results.getDecompiledFunction().getC() : results.getErrorMessage());
			}
		}
	}

	// A hex address, or a class whose vtable is found through its RTTI type descriptor
	private List<Address> resolve(String arg) throws Exception {
		List<Address> addresses = new ArrayList<>();
		if (arg.matches("(0x)?[0-9a-fA-F]+")) {
			addresses.add(toAddr(Long.parseLong(arg.replace("0x", ""), 16)));
			return addresses;
		}

		String[] parts = arg.split("::");
		StringBuilder mangled = new StringBuilder(".?AV" + parts[parts.length - 1] + "@");
		for (int i = parts.length - 2; i >= 0; i--) {
			mangled.append(parts[i]).append("@");
		}
		mangled.append("@");

		Memory memory = currentProgram.getMemory();
		Address name = memory.findBytes(currentProgram.getMinAddress(), (mangled + "\0").getBytes(), null, true, monitor);
		if (name == null) {
			println("== " + arg + ": no RTTI name " + mangled);
			return addresses;
		}

		// TypeDescriptor starts 8 bytes before its name; the CompleteObjectLocator holds it at +12 with signature 0
		Address typeDescriptor = name.subtract(8);
		Address col = null;
		for (Address hit = find(currentProgram.getMinAddress(), typeDescriptor); hit != null; hit = find(hit.add(1), typeDescriptor)) {
			if (memory.getInt(hit.subtract(12)) == 0 && memory.getInt(hit.subtract(8)) == 0) {
				col = hit.subtract(12);
				break;
			}
		}
		Address colRef = col == null ? null : find(currentProgram.getMinAddress(), col);
		if (colRef == null) {
			println("== " + arg + ": no vtable");
			return addresses;
		}

		// The vtable follows the pointer to its COL and runs until the next non-code pointer
		Address vtable = colRef.add(4);
		println("== " + arg + " vtable @ " + vtable);
		for (int slot = 0; slot < 64; slot++) {
			Address entry = toAddr(memory.getInt(vtable.add(slot * 4L)) & 0xffffffffL);
			if (memory.getBlock(entry) == null || !memory.getBlock(entry).isExecute()) {
				break;
			}
			println("   slot " + slot + ": " + entry);
			addresses.add(entry);
		}
		return addresses;
	}

	private Address find(Address start, Address value) throws Exception {
		long v = value.getOffset();
		byte[] bytes = { (byte) v, (byte) (v >> 8), (byte) (v >> 16), (byte) (v >> 24) };
		return currentProgram.getMemory().findBytes(start, bytes, null, true, monitor);
	}
}
