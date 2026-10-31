package com.lotusdugun.sisifos;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;

/**
 * Serves the share cards the game writes to cache/share/ as content://<app id>.sisifosshare/<name>,
 * read-only, to the app the player picked in the share sheet (the intent grants it the uri).
 * Nothing else is reachable: one flat folder, no paths.
 */
public class SisifosShareProvider extends ContentProvider {
    @Override public boolean onCreate() { return true; }

    private File file(Uri uri) {
        String name = uri.getLastPathSegment();
        if (name == null || name.contains("/") || name.contains("..") || getContext() == null) return null;
        File f = new File(new File(getContext().getCacheDir(), "share"), name);
        return f.isFile() ? f : null;
    }

    @Override public String getType(Uri uri) {
        String n = uri.getLastPathSegment();
        if (n != null && n.endsWith(".png")) return "image/png";
        if (n != null && (n.endsWith(".jpg") || n.endsWith(".jpeg"))) return "image/jpeg";
        return "application/octet-stream";
    }

    @Override public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        if (mode == null || !mode.startsWith("r") || mode.contains("w")) throw new FileNotFoundException("read only");
        File f = file(uri);
        if (f == null) throw new FileNotFoundException(String.valueOf(uri));
        return ParcelFileDescriptor.open(f, ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder) {
        File f = file(uri);
        if (projection == null) projection = new String[] { OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE };
        String[] cols = new String[projection.length];
        Object[] vals = new Object[projection.length];
        int n = 0;
        for (String c : projection) {
            if (OpenableColumns.DISPLAY_NAME.equals(c)) { cols[n] = c; vals[n++] = f != null ? f.getName() : uri.getLastPathSegment(); }
            else if (OpenableColumns.SIZE.equals(c)) { cols[n] = c; vals[n++] = f != null ? f.length() : 0L; }
        }
        String[] outCols = new String[n];
        Object[] outVals = new Object[n];
        System.arraycopy(cols, 0, outCols, 0, n);
        System.arraycopy(vals, 0, outVals, 0, n);
        MatrixCursor cursor = new MatrixCursor(outCols, 1);
        cursor.addRow(outVals);
        return cursor;
    }

    @Override public Uri insert(Uri uri, ContentValues values) { throw new UnsupportedOperationException(); }
    @Override public int delete(Uri uri, String selection, String[] selectionArgs) { throw new UnsupportedOperationException(); }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) { throw new UnsupportedOperationException(); }
}
